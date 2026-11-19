namespace DormitoryManagementSystem.Services;

public sealed class VnpayOptions
{
    public const string SectionName = "Vnpay";
    public bool Enabled { get; set; }
    public string MerchantCode { get; set; } = string.Empty;
    public string HashSecret { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;

    internal bool HasCredentials => MerchantCode.Length == 8 &&
        MerchantCode.All(char.IsAsciiLetterOrDigit) && !string.IsNullOrWhiteSpace(HashSecret);

    internal Uri? ReturnUri()
    {
        if (!Enabled || !HasCredentials ||
            !Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var origin) ||
            (origin.Scheme != Uri.UriSchemeHttps && !(origin.Scheme == Uri.UriSchemeHttp && origin.IsLoopback)) ||
            origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0 || origin.UserInfo.Length != 0)
            return null;

        return new Uri(origin, "/payments/vnpay/return");
    }
}
