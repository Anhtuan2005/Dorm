using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public sealed record BillingLedgerLine(string Description, string ChargeType, decimal Amount);

public sealed record BillingLedgerTransaction(
    long Id,
    string Code,
    decimal Amount,
    string MethodLabel,
    DateTime ReceivedAt,
    DateTime RecordedAt,
    string RecordedBy,
    string? ExternalReference,
    string? Note,
    bool IsReversal,
    bool HasBeenReversed,
    string? ReversedCode);

public sealed record BillingInvoiceSnapshot(
    long Id,
    string Code,
    string PeriodName,
    DateOnly DueDate,
    DateTime IssuedAt,
    BillingLedgerSummary Summary,
    IReadOnlyList<BillingLedgerLine> Lines,
    IReadOnlyList<BillingLedgerTransaction> Transactions);

public interface IBillingLedgerService
{
    Task<BillingQueueViewModel> GetQueueAsync(BillingQueueQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingInvoiceSnapshot>> GetStudentInvoicesAsync(int studentProfileId, CancellationToken cancellationToken = default);
    Task<decimal> GetOutstandingBalanceAsync(int studentProfileId, CancellationToken cancellationToken = default);
    Task<InvoiceIssueResult> IssueInvoicesAsync(IssueInvoicesInput input, CancellationToken cancellationToken = default);
    Task<BillingOperationResult> RecordPaymentAsync(RecordPaymentInput input, string managerUserId, CancellationToken cancellationToken = default);
    Task<BillingOperationResult> ReversePaymentAsync(ReversePaymentInput input, string managerUserId, CancellationToken cancellationToken = default);
}
