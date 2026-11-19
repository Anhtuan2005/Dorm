using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using DormitoryManagementSystem.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DormitoryManagementSystem.Services;

public sealed record VnpayStartResult(string? CheckoutUrl, string? Error = null);

public sealed record VnpayPaymentStatus(
    string Reference, long InvoiceId, string InvoiceCode, decimal Amount, VnpayCheckoutState State, bool IsExpired);

public sealed record VnpayIpnReply(
    [property: JsonPropertyName("RspCode")] string RspCode,
    [property: JsonPropertyName("Message")] string Message)
{
    internal static readonly VnpayIpnReply Confirmed = new("00", "Confirm Success");
    internal static readonly VnpayIpnReply UnknownOrder = new("01", "Order not found");
    internal static readonly VnpayIpnReply AlreadyConfirmed = new("02", "Order already confirmed");
    internal static readonly VnpayIpnReply InvalidAmount = new("04", "Invalid amount");
    internal static readonly VnpayIpnReply InvalidSignature = new("97", "Invalid signature");
    internal static readonly VnpayIpnReply Retry = new("99", "Unable to confirm");
}

internal sealed record VnpayNotification(
    string Reference, long AmountTimes100, string ResponseCode, string ProviderState, string? ProviderTransactionNumber, DateTime? PaidAtUtc)
{
    public bool Succeeded => ResponseCode == "00" && ProviderState == "00";
    public bool RequiresReview => ResponseCode is "00" or "07" || ProviderState is "00" or "04" or "07";
}

public sealed class VnpayCheckoutService(BillingLedgerService ledger, IOptions<VnpayOptions> options)
{
    private const string SandboxEndpoint = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    private readonly VnpayOptions configuration = options.Value;

    public bool CanStart => configuration.ReturnUri() is not null;

    public async Task<VnpayStartResult> StartAsync(
        string userId, long invoiceId, IPAddress? clientIp, CancellationToken cancellationToken = default)
    {
        var returnUri = configuration.ReturnUri();
        if (returnUri is null)
            return new(null, "Thanh toán VNPAY hiện chưa được bật.");

        var (checkout, error) = await ledger.PrepareVnpayCheckoutAsync(userId, invoiceId, configuration.MerchantCode, cancellationToken);
        if (checkout is null)
            return new(null, error);

        var fields = new Dictionary<string, string>
        {
            ["vnp_Amount"] = ((long)(checkout.Amount * 100m)).ToString(CultureInfo.InvariantCulture),
            ["vnp_Command"] = "pay",
            ["vnp_CreateDate"] = FormatTime(checkout.CreatedAtUtc),
            ["vnp_CurrCode"] = "VND",
            ["vnp_ExpireDate"] = FormatTime(checkout.ExpiresAtUtc),
            ["vnp_IpAddr"] = ClientAddress(clientIp),
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = "Thanh toan hoa don KTX",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUri.AbsoluteUri,
            ["vnp_TmnCode"] = checkout.MerchantCode,
            ["vnp_TxnRef"] = checkout.Reference,
            ["vnp_Version"] = "2.1.0"
        };
        var payload = Canonicalize(fields);
        var signature = Convert.ToHexString(Sign(payload)).ToLowerInvariant();
        return new($"{SandboxEndpoint}?{payload}&vnp_SecureHash={signature}");
    }

    public async Task<VnpayIpnReply> ReceiveIpnAsync(
        IEnumerable<KeyValuePair<string, StringValues>> query, CancellationToken cancellationToken = default)
    {
        var (notification, error) = ReadNotification(query);
        return notification is null
            ? error!
            : await ledger.ConfirmVnpayCheckoutAsync(configuration.MerchantCode, notification, cancellationToken);
    }

    public Task<VnpayPaymentStatus?> ReadReturnAsync(
        string userId, IEnumerable<KeyValuePair<string, StringValues>> query, CancellationToken cancellationToken = default)
    {
        var (notification, _) = ReadNotification(query);
        return notification is null ? Task.FromResult<VnpayPaymentStatus?>(null)
            : ledger.ReadVnpayCheckoutAsync(userId, notification.Reference, configuration.MerchantCode, notification.AmountTimes100, cancellationToken);
    }

    public Task<VnpayPaymentStatus?> GetStatusAsync(
        string userId, string reference, CancellationToken cancellationToken = default) =>
        Guid.TryParseExact(reference, "N", out var id)
            ? ledger.ReadVnpayCheckoutAsync(userId, id.ToString("N"), null, null, cancellationToken)
            : Task.FromResult<VnpayPaymentStatus?>(null);

    private (VnpayNotification? Notification, VnpayIpnReply? Error) ReadNotification(
        IEnumerable<KeyValuePair<string, StringValues>> query)
    {
        if (!configuration.HasCredentials) return (null, VnpayIpnReply.Retry);

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, values) in query)
        {
            if (!key.StartsWith("vnp_", StringComparison.Ordinal)) continue;
            if (values.Count != 1 || !fields.TryAdd(key, values[0] ?? string.Empty))
                return (null, VnpayIpnReply.InvalidSignature);
        }

        if (!fields.Remove("vnp_SecureHash", out var hash) || hash.Length != 128 || !hash.All(Uri.IsHexDigit))
            return (null, VnpayIpnReply.InvalidSignature);
        fields.Remove("vnp_SecureHashType");
        if (!CryptographicOperations.FixedTimeEquals(Sign(Canonicalize(fields)), Convert.FromHexString(hash)) ||
            fields.GetValueOrDefault("vnp_TmnCode") != configuration.MerchantCode)
            return (null, VnpayIpnReply.InvalidSignature);

        var amount = fields.GetValueOrDefault("vnp_Amount");
        if (!TryReadDigits(amount, 12, out var scaledAmount) || scaledAmount <= 0 ||
            (fields.TryGetValue("vnp_CurrCode", out var currency) && currency != "VND"))
            return (null, VnpayIpnReply.InvalidAmount);

        var response = fields.GetValueOrDefault("vnp_ResponseCode");
        var state = fields.GetValueOrDefault("vnp_TransactionStatus");
        if (!Guid.TryParseExact(fields.GetValueOrDefault("vnp_TxnRef"), "N", out var reference) ||
            !IsStatusCode(response) || !IsStatusCode(state))
            return (null, VnpayIpnReply.Retry);

        var hasProviderNumber = TryReadDigits(fields.GetValueOrDefault("vnp_TransactionNo"), 15, out var providerNumber) && providerNumber > 0;
        if (response == "00" && state == "00" && !hasProviderNumber)
            return (null, VnpayIpnReply.Retry);

        return (new VnpayNotification(reference.ToString("N"), scaledAmount, response!, state!,
            hasProviderNumber ? providerNumber.ToString(CultureInfo.InvariantCulture) : null,
            ReadPayDate(fields.GetValueOrDefault("vnp_PayDate"))), null);
    }

    private static string Canonicalize(IEnumerable<KeyValuePair<string, string>> fields) =>
        string.Join('&', fields.Where(x => x.Value.Length != 0).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));

    private byte[] Sign(string payload) =>
        HMACSHA512.HashData(Encoding.UTF8.GetBytes(configuration.HashSecret), Encoding.UTF8.GetBytes(payload));

    private static string FormatTime(DateTime utc) =>
        utc.AddHours(7).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private static DateTime? ReadPayDate(string? local) =>
        DateTime.TryParseExact(local, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? new DateTimeOffset(date, TimeSpan.FromHours(7)).UtcDateTime : null;

    private static bool IsStatusCode(string? code) => code is { Length: 2 } && code.All(char.IsAsciiDigit);

    private static bool TryReadDigits(string? value, int maxLength, out long parsed)
    {
        parsed = 0;
        return value is { Length: > 0 } && value.Length <= maxLength && value.All(char.IsAsciiDigit) &&
            long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed);
    }

    private static string ClientAddress(IPAddress? address) =>
        address is null || IPAddress.IsLoopback(address) ? IPAddress.Loopback.ToString()
        : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
}
