using System.Data;
using DormitoryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed partial class BillingLedgerService
{
    internal Task<(VnpayCheckout? Checkout, string? Error)> PrepareVnpayCheckoutAsync(
        string userId, long invoiceId, string merchantCode, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync<(VnpayCheckout? Checkout, string? Error)>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var invoice = await db.Invoices
                .Include(x => x.BillingPeriod).Include(x => x.Transactions)
                .SingleOrDefaultAsync(x => x.Id == invoiceId && x.StudentProfile.UserId == userId, cancellationToken);
            if (invoice is null)
                return (null, "Không tìm thấy hóa đơn của bạn.");

            var summary = Summarize(invoice);
            if (!summary.CanCollect)
                return (null, "Hóa đơn này hiện không thể thanh toán qua VNPAY.");
            if (!VnpayCheckout.SupportsAmount(summary.Balance))
                return (null, "Số tiền còn phải trả không đáp ứng điều kiện thanh toán qua VNPAY. Vui lòng xem hướng dẫn thanh toán trên hóa đơn.");

            if (await db.VnpayCheckouts.AnyAsync(x =>
                    x.InvoiceId == invoice.Id && x.State == VnpayCheckoutState.NeedsReview, cancellationToken))
                return (null, "Hóa đơn có giao dịch VNPAY cần đối soát. Vui lòng liên hệ Ban quản lý.");

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var active = await PendingVnpayCheckouts(invoice.Id, now).FirstOrDefaultAsync(cancellationToken);
            if (active is not null)
                return active.Amount == summary.Balance && active.MerchantCode == merchantCode
                    ? (active, null)
                    : (null, "Hóa đơn có giao dịch VNPAY đang chờ xác nhận. Vui lòng kiểm tra lại sau.");

            var checkout = new VnpayCheckout
            {
                Reference = Guid.NewGuid().ToString("N"),
                MerchantCode = merchantCode,
                InvoiceId = invoice.Id,
                Amount = summary.Balance,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(15),
                State = VnpayCheckoutState.Pending
            };
            db.VnpayCheckouts.Add(checkout);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (checkout, null);
        });

    private IQueryable<VnpayCheckout> PendingVnpayCheckouts(long invoiceId, DateTime nowUtc) =>
        db.VnpayCheckouts.Where(x => x.InvoiceId == invoiceId &&
            x.State == VnpayCheckoutState.Pending && x.ExpiresAtUtc > nowUtc);

    internal async Task<VnpayPaymentStatus?> ReadVnpayCheckoutAsync(
        string userId, string reference, string? merchantCode, long? amountTimes100, CancellationToken cancellationToken)
    {
        var payment = await db.VnpayCheckouts.AsNoTracking().Include(x => x.Invoice)
            .SingleOrDefaultAsync(x => x.Reference == reference && x.Invoice.StudentProfile.UserId == userId, cancellationToken);
        if (payment is null ||
            (merchantCode is not null && payment.MerchantCode != merchantCode) ||
            (amountTimes100.HasValue && payment.Amount * 100m != amountTimes100.Value))
            return null;

        return new(payment.Reference, payment.InvoiceId, payment.Invoice.Code, payment.Amount, payment.State,
            payment.State == VnpayCheckoutState.Pending && payment.ExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime);
    }

    internal Task<VnpayIpnReply> ConfirmVnpayCheckoutAsync(
        string merchantCode, VnpayNotification notification, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var payment = await db.VnpayCheckouts
                .Include(x => x.Invoice).ThenInclude(x => x.StudentProfile)
                .Include(x => x.Invoice).ThenInclude(x => x.BillingPeriod)
                .Include(x => x.Invoice).ThenInclude(x => x.Transactions)
                .SingleOrDefaultAsync(x => x.Reference == notification.Reference && x.MerchantCode == merchantCode, cancellationToken);
            if (payment is null) return VnpayIpnReply.UnknownOrder;
            if (payment.Amount * 100m != notification.AmountTimes100) return VnpayIpnReply.InvalidAmount;
            if (payment.State != VnpayCheckoutState.Pending) return VnpayIpnReply.AlreadyConfirmed;
            if (notification.ProviderState == "01") return VnpayIpnReply.Retry;

            if (notification.ProviderTransactionNumber is { } providerNumber &&
                await db.VnpayCheckouts.AnyAsync(x => x.MerchantCode == merchantCode &&
                    x.ProviderTransactionNumber == providerNumber && x.Reference != payment.Reference, cancellationToken))
                return VnpayIpnReply.Retry;

            var now = timeProvider.GetUtcNow().UtcDateTime;
            payment.ConfirmedAtUtc = now;
            payment.ResponseCode = notification.ResponseCode;
            payment.ProviderState = notification.ProviderState;
            payment.ProviderTransactionNumber = notification.ProviderTransactionNumber;
            var summary = Summarize(payment.Invoice);
            if (notification.Succeeded && summary.CanCollect && payment.Amount == summary.Balance)
            {
                var paidAt = notification.PaidAtUtc is { } reportedAt &&
                    reportedAt >= payment.CreatedAtUtc.AddSeconds(-1) && reportedAt <= now.AddMinutes(5) ? reportedAt : now;
                await AppendCollectionAsync(payment.Invoice, new PaymentTransaction
                {
                    Amount = payment.Amount,
                    Method = PaymentMethod.VnpaySandbox,
                    ReceivedAt = paidAt,
                    ExternalReference = notification.ProviderTransactionNumber,
                    IdempotencyKey = $"{VnpayIdempotencyPrefix}{payment.Reference}"
                }, now, cancellationToken);
                payment.State = VnpayCheckoutState.Settled;
            }
            else if (notification.RequiresReview)
            {
                payment.State = VnpayCheckoutState.NeedsReview;
                AddNotification(payment.Invoice, now, "Giao dịch VNPAY cần kiểm tra",
                    $"Vui lòng liên hệ Ban quản lý để đối soát hóa đơn {payment.Invoice.Code} trước khi thử lại.");
            }
            else
            {
                payment.State = VnpayCheckoutState.Failed;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (live is not null && payment.State is (VnpayCheckoutState.Settled or VnpayCheckoutState.NeedsReview))
                await live.NotifyOperationsAsync(payment.Invoice.StudentProfile.UserId);
            return VnpayIpnReply.Confirmed;
        });
}
