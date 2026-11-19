using System.Net;
using System.Security.Cryptography;
using System.Text;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace DormitoryManagementSystem.IntegrationTests;

public sealed class VnpayCheckoutIntegrationTests
{
    private const string TestMerchant = "TESTKTX1";
    private const string TestSecret = "fixture-only-not-a-merchant-key";
    private const string OwnerId = "student-vnpay-test";
    private const string ManagerId = "manager-vnpay-test";
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 8, 20, 0, TimeSpan.Zero);

    [SqlServerFact]
    public async Task Checkout_redirect_is_signed_for_the_owners_outstanding_balance()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var invoice = await CreateInvoiceAsync(database);
        await using var context = database.CreateContext();
        var result = await Checkout(context).StartAsync(OwnerId, invoice.Id, IPAddress.Loopback);

        Assert.NotNull(result.CheckoutUrl);
        var uri = new Uri(result.CheckoutUrl);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal("sandbox.vnpayment.vn", uri.Host);
        Assert.Equal("/paymentv2/vpcpay.html", uri.AbsolutePath);
        Assert.Equal("150000000", query["vnp_Amount"]);
        Assert.Equal("20261117152000", query["vnp_CreateDate"]);
        Assert.Equal("20261117153500", query["vnp_ExpireDate"]);
        Assert.Equal("https://ktx.example.test/payments/vnpay/return", query["vnp_ReturnUrl"]);
        Assert.Matches("^[a-f0-9]{32}$", query["vnp_TxnRef"].ToString());
        var signedPayload = uri.Query[1..uri.Query.LastIndexOf("&vnp_SecureHash=", StringComparison.Ordinal)];
        Assert.Contains("vnp_OrderInfo=Thanh+toan+hoa+don+KTX", signedPayload);
        Assert.Equal(Sign(signedPayload), query["vnp_SecureHash"].ToString());
    }

    [SqlServerFact]
    public async Task An_outstanding_balance_below_the_gateway_minimum_does_not_create_a_checkout()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var invoice = await CreateInvoiceAsync(database, totalAmount: 29_999m);
        await using var context = database.CreateContext();

        var result = await Checkout(context).StartAsync(OwnerId, invoice.Id, IPAddress.Loopback);

        Assert.Null(result.CheckoutUrl);
        Assert.NotNull(result.Error);
        Assert.Empty(context.VnpayCheckouts);
    }

    [SqlServerFact]
    public async Task A_signed_browser_return_is_read_only_and_only_ipn_records_the_collection()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var invoice = await CreateInvoiceAsync(database);
        await using var context = database.CreateContext();
        var checkout = Checkout(context);
        var reference = await StartAsync(checkout, invoice.Id);
        var notification = Callback(reference);
        var ledger = new BillingLedgerService(context, new FixedTimeProvider(Now));

        var browser = await checkout.ReadReturnAsync(OwnerId, notification);
        Assert.Equal(VnpayCheckoutState.Pending, browser?.State);
        var before = Assert.Single(await ledger.GetStudentInvoicesAsync(invoice.StudentProfileId));
        Assert.Equal(1_500_000m, before.Summary.Balance);
        Assert.Single(before.Transactions);

        Assert.Equal("00", (await checkout.ReceiveIpnAsync(notification)).RspCode);
        var after = Assert.Single(await ledger.GetStudentInvoicesAsync(invoice.StudentProfileId));
        Assert.Equal(0, after.Summary.Balance);
        var entry = Assert.Single(after.Transactions, x => x.MethodLabel == "VNPAY Sandbox");
        Assert.Equal("123456789", entry.ExternalReference);
        Assert.Equal(VnpayCheckoutState.Settled, (await checkout.GetStatusAsync(OwnerId, reference))?.State);
        var staff = (await ledger.GetQueueAsync(new BillingQueueQuery { SelectedId = invoice.Id })).SelectedInvoice!;
        Assert.False((await ledger.ReversePaymentAsync(new ReversePaymentInput
        {
            TransactionId = entry.Id, Reason = "Hoàn tác thử khoản thu VNPAY",
            IdempotencyKey = "fixture-reverse-online", RowVersion = staff.RowVersion
        }, ManagerId)).Success);
    }

    [SqlServerFact]
    public async Task Manual_collection_waits_for_active_checkout_and_a_late_ipn_cannot_overpay()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var invoice = await CreateInvoiceAsync(database);
        await using var context = database.CreateContext();
        var checkout = Checkout(context);
        var reference = await StartAsync(checkout, invoice.Id);
        var cash = new RecordPaymentInput
        {
            InvoiceId = invoice.Id, Amount = 1_500_000m, Method = "Cash", ReceivedAt = Now.UtcDateTime,
            IdempotencyKey = "fixture-manual-collection", RowVersion = Convert.ToBase64String(invoice.RowVersion)
        };
        var ledger = new BillingLedgerService(context, new FixedTimeProvider(Now));
        Assert.False((await ledger.RecordPaymentAsync(cash, ManagerId)).Success);

        var afterExpiry = new FixedTimeProvider(Now.AddMinutes(16));
        ledger = new BillingLedgerService(context, afterExpiry);
        Assert.True((await ledger.RecordPaymentAsync(cash, ManagerId)).Success);
        checkout = Checkout(context, afterExpiry);
        Assert.Equal("00", (await checkout.ReceiveIpnAsync(Callback(reference))).RspCode);
        Assert.Equal(VnpayCheckoutState.NeedsReview, (await checkout.GetStatusAsync(OwnerId, reference))?.State);
        var current = Assert.Single(await ledger.GetStudentInvoicesAsync(invoice.StudentProfileId));
        Assert.Equal(1_525_000m, current.Summary.Collected);
        Assert.DoesNotContain(current.Transactions, x => x.MethodLabel == "VNPAY Sandbox");
        var staff = (await ledger.GetQueueAsync(new BillingQueueQuery { SelectedId = invoice.Id })).SelectedInvoice!;
        Assert.Equal(reference, Assert.Single(staff.VnpayReviews).Reference);
    }

    [SqlServerFact]
    public async Task Other_students_and_invalid_callbacks_cannot_change_an_invoice()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var invoice = await CreateInvoiceAsync(database);
        await using var context = database.CreateContext();
        var checkout = Checkout(context);
        const string otherAccount = "another-student";
        Assert.Null((await checkout.StartAsync(otherAccount, invoice.Id, IPAddress.Loopback)).CheckoutUrl);
        var reference = await StartAsync(checkout, invoice.Id);
        Assert.Null(await checkout.GetStatusAsync(otherAccount, reference));
        Assert.Null(await checkout.ReadReturnAsync(otherAccount, Callback(reference)));

        var tampered = Callback(reference);
        tampered["vnp_Amount"] = "1";
        Assert.Equal("97", (await checkout.ReceiveIpnAsync(tampered)).RspCode);
        var duplicateField = Callback(reference);
        duplicateField["vnp_Amount"] = new StringValues(["150000000", "150000000"]);
        Assert.Equal("97", (await checkout.ReceiveIpnAsync(duplicateField)).RspCode);
        Assert.Equal("97", (await checkout.ReceiveIpnAsync(Callback(reference, merchant: "TESTKTX2"))).RspCode);
        Assert.Equal("04", (await checkout.ReceiveIpnAsync(Callback(reference, amount: "149999900"))).RspCode);
        Assert.Equal("01", (await checkout.ReceiveIpnAsync(Callback(Guid.NewGuid().ToString("N")))).RspCode);
        Assert.Equal(VnpayCheckoutState.Pending, (await checkout.GetStatusAsync(OwnerId, reference))?.State);
        var invoiceAfter = Assert.Single(await new BillingLedgerService(context, new FixedTimeProvider(Now))
            .GetStudentInvoicesAsync(invoice.StudentProfileId));
        Assert.Equal(1_500_000m, invoiceAfter.Summary.Balance);
    }

    [SqlServerFact]
    public async Task Concurrent_checkout_and_ipn_retries_create_one_link_and_one_collection()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var invoice = await CreateInvoiceAsync(database);
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var first = Checkout(firstContext);
        var second = Checkout(secondContext);
        var references = await Task.WhenAll(StartAsync(first, invoice.Id), StartAsync(second, invoice.Id));
        Assert.Equal(references[0], references[1]);

        var ipn = Callback(references[0]);
        var replies = await Task.WhenAll(first.ReceiveIpnAsync(ipn), second.ReceiveIpnAsync(ipn));
        Assert.Equal(new[] { "00", "02" }, replies.Select(x => x.RspCode).OrderBy(x => x, StringComparer.Ordinal));
        var ledger = new BillingLedgerService(firstContext, new FixedTimeProvider(Now));
        var after = Assert.Single(await ledger.GetStudentInvoicesAsync(invoice.StudentProfileId));
        Assert.Equal(1_525_000m, after.Summary.Collected);
        Assert.Single(after.Transactions, x => x.MethodLabel == "VNPAY Sandbox");
    }

    [SqlServerFact]
    public async Task A_confirmed_failure_allows_retry_without_recording_a_collection()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var invoice = await CreateInvoiceAsync(database);
        await using var context = database.CreateContext();
        var checkout = Checkout(context);
        var reference = await StartAsync(checkout, invoice.Id);
        Assert.Equal("99", (await checkout.ReceiveIpnAsync(Callback(reference, providerState: "01", transactionNumber: "0"))).RspCode);
        Assert.Equal("00", (await checkout.ReceiveIpnAsync(Callback(reference, responseCode: "24", providerState: "02", transactionNumber: "0"))).RspCode);
        Assert.Equal(VnpayCheckoutState.Failed, (await checkout.GetStatusAsync(OwnerId, reference))?.State);
        Assert.NotEqual(reference, await StartAsync(checkout, invoice.Id));
        var invoiceAfter = Assert.Single(await new BillingLedgerService(context, new FixedTimeProvider(Now))
            .GetStudentInvoicesAsync(invoice.StudentProfileId));
        Assert.Equal(1_500_000m, invoiceAfter.Summary.Balance);
    }

    private static async Task<string> StartAsync(VnpayCheckoutService checkout, long invoiceId)
    {
        var result = await checkout.StartAsync(OwnerId, invoiceId, IPAddress.Loopback);
        Assert.NotNull(result.CheckoutUrl);
        return QueryHelpers.ParseQuery(new Uri(result.CheckoutUrl).Query)["vnp_TxnRef"].ToString();
    }

    private static Dictionary<string, StringValues> Callback(
        string reference, string amount = "150000000", string responseCode = "00",
        string providerState = "00", string transactionNumber = "123456789", string merchant = TestMerchant)
    {
        var callback = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = amount,
            ["vnp_BankCode"] = "NCB",
            ["vnp_OrderInfo"] = "Thanh toan hoa don KTX",
            ["vnp_PayDate"] = "20261117152100",
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TmnCode"] = merchant,
            ["vnp_TransactionNo"] = transactionNumber,
            ["vnp_TransactionStatus"] = providerState,
            ["vnp_TxnRef"] = reference
        };
        var payload = string.Join('&', callback.Select(x => $"{x.Key}={WebUtility.UrlEncode(x.Value)}"));
        var fields = callback.ToDictionary(x => x.Key, x => new StringValues(x.Value));
        fields.Add("vnp_SecureHash", Sign(payload));
        return fields;
    }

    private static string Sign(string payload) =>
        Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(TestSecret), Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();

    private static VnpayCheckoutService Checkout(DormitoryDbContext context, TimeProvider? clock = null) =>
        new(new BillingLedgerService(context, clock ?? new FixedTimeProvider(Now)),
            Options.Create(new VnpayOptions
            {
                Enabled = true,
                MerchantCode = TestMerchant,
                HashSecret = TestSecret,
                PublicBaseUrl = "https://ktx.example.test"
            }));

    private static async Task<Invoice> CreateInvoiceAsync(SqlServerTestDatabase database, decimal totalAmount = 1_525_000m)
    {
        await using var context = database.CreateContext();
        var invoice = new Invoice
        {
            Code = "HD-VNPAY-TEST",
            StudentProfile = new StudentProfile
            {
                StudentCode = "SV-VNPAY-TEST",
                FullName = "Sinh viên kiểm thử",
                User = new ApplicationUser { Id = OwnerId, UserName = "checkout-test", FullName = "Sinh viên kiểm thử" }
            },
            BillingPeriod = new BillingPeriod
            {
                Code = "2026-11",
                Name = "Tháng 11/2026",
                StartDate = new DateOnly(2026, 11, 1),
                EndDate = new DateOnly(2026, 11, 30),
                DueDate = new DateOnly(2026, 11, 25)
            },
            TotalAmount = totalAmount,
            IssuedAt = Now.UtcDateTime.AddDays(-1),
            Status = InvoiceStatus.Issued,
            Lines = [new InvoiceLine { Description = "Phí KTX", ChargeType = "Room", Amount = totalAmount }],
            Transactions = [new PaymentTransaction
            {
                Code = "THU-BASELINE-TEST",
                IdempotencyKey = "fixture-opening-collection",
                Method = PaymentMethod.Cash,
                Amount = 25_000m,
                ReceivedAt = Now.UtcDateTime.AddDays(-1),
                RecordedAt = Now.UtcDateTime.AddDays(-1)
            }]
        };
        context.Invoices.Add(invoice);
        context.Users.Add(new ApplicationUser { Id = ManagerId, UserName = "checkout-manager-test", FullName = "Quản lý kiểm thử" });
        await context.SaveChangesAsync();
        return invoice;
    }
}
