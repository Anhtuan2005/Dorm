using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Services;

public enum BillingPaymentState { Awaiting, Partial, Paid, Overdue, Cancelled }

public sealed record BillingLedgerSummary(
    decimal Total,
    decimal Collected,
    decimal Balance,
    BillingPaymentState State,
    string StatusLabel,
    string StatusClass,
    bool CanCollect);

public static class BillingLedgerPolicy
{
    public static BillingLedgerSummary Summarize(
        decimal total,
        IEnumerable<decimal> transactionAmounts,
        DateOnly dueDate,
        DateOnly businessDate,
        InvoiceStatus lifecycleStatus)
    {
        var collected = transactionAmounts.Sum();
        var balance = Math.Max(0, total - collected);

        if (lifecycleStatus == InvoiceStatus.Cancelled)
            return new(total, collected, balance, BillingPaymentState.Cancelled, "Đã hủy", "cancelled", false);

        if (balance == 0)
            return new(total, collected, 0, BillingPaymentState.Paid, "Đã thu đủ", "paid", false);

        if (dueDate < businessDate)
            return new(total, collected, balance, BillingPaymentState.Overdue, "Quá hạn", "overdue", true);

        if (collected > 0)
            return new(total, collected, balance, BillingPaymentState.Partial, "Thu một phần", "partial", true);

        return new(total, 0, balance, BillingPaymentState.Awaiting, "Chờ thu", "issued", true);
    }
}
