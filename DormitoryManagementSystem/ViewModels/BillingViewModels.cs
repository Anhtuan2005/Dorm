using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.ViewModels;

public sealed class BillingQueueQuery
{
    public string? Search { get; set; }
    public int? PeriodId { get; set; }
    public string? Status { get; set; }
    public string? Building { get; set; }
    [Range(0, double.MaxValue)] public decimal? MinBalance { get; set; }
    [Range(0, double.MaxValue)] public decimal? MaxBalance { get; set; }
    public long? SelectedId { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed record BillingFilterOption(string Value, string Label);

public sealed record BillingQueueSummaryViewModel(
    int OutstandingCount,
    decimal OutstandingAmount,
    int OverdueCount,
    decimal OverdueAmount,
    int DueSoonCount,
    decimal CollectedThisMonth);

public sealed record BillingQueueItemViewModel(
    long Id,
    string Code,
    string StudentCode,
    string StudentName,
    string Initials,
    string RoomCode,
    string PeriodName,
    DateOnly DueDate,
    decimal TotalAmount,
    decimal CollectedAmount,
    decimal Balance,
    string StatusLabel,
    string StatusClass,
    bool IsSelected);

public sealed record BillingInvoiceLineViewModel(string Description, string ChargeType, decimal Amount);

public sealed record BillingTransactionViewModel(
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

public sealed class BillingInvoiceDetailViewModel
{
    public long Id { get; init; }
    public int StudentId { get; init; }
    public required string Code { get; init; }
    public required string StudentCode { get; init; }
    public required string StudentName { get; init; }
    public required string Faculty { get; init; }
    public required string PhoneNumber { get; init; }
    public required string RoomCode { get; init; }
    public required string BuildingName { get; init; }
    public required string PeriodName { get; init; }
    public DateOnly DueDate { get; init; }
    public DateTime IssuedAt { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal CollectedAmount { get; init; }
    public decimal Balance { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusClass { get; init; }
    public bool CanCollect { get; init; }
    public required string RowVersion { get; init; }
    public required IReadOnlyList<BillingInvoiceLineViewModel> Lines { get; init; }
    public required IReadOnlyList<BillingTransactionViewModel> Transactions { get; init; }
}

public sealed class BillingQueueViewModel
{
    public required BillingQueueQuery Query { get; init; }
    public required BillingQueueSummaryViewModel Summary { get; init; }
    public required IReadOnlyList<BillingQueueItemViewModel> Items { get; init; }
    public required IReadOnlyList<BillingFilterOption> Periods { get; init; }
    public required IReadOnlyList<BillingFilterOption> Buildings { get; init; }
    public DateTime DefaultReceivedAtLocal { get; init; }
    public BillingInvoiceDetailViewModel? SelectedInvoice { get; init; }
    public int TotalItems { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));
    public DateOnly SuggestedPeriodStart { get; init; }
    public DateOnly SuggestedPeriodEnd { get; init; }
    public DateOnly SuggestedDueDate { get; init; }
}

public sealed class IssueInvoicesInput
{
    [Required, StringLength(12, MinimumLength = 3)] public string PeriodCode { get; set; } = string.Empty;
    [Required, StringLength(80)] public string PeriodName { get; set; } = string.Empty;
    [Required] public DateOnly? StartDate { get; set; }
    [Required] public DateOnly? EndDate { get; set; }
    [Required] public DateOnly? DueDate { get; set; }
    [StringLength(4)] public string? BuildingCode { get; set; }
    [Range(typeof(decimal), "0", "999999999")] public decimal ElectricityAmount { get; set; }
    [Range(typeof(decimal), "0", "999999999")] public decimal WaterAmount { get; set; }
    [Range(typeof(decimal), "0", "999999999")] public decimal ServiceAmount { get; set; }
}

public sealed class RecordPaymentInput
{
    [Range(1, long.MaxValue)] public long InvoiceId { get; set; }

    [Range(typeof(decimal), "1", "999999999", ErrorMessage = "Số tiền phải lớn hơn 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phương thức thu.")]
    public string Method { get; set; } = "BankTransfer";

    [Required(ErrorMessage = "Vui lòng nhập thời điểm nhận tiền.")]
    public DateTime? ReceivedAt { get; set; }

    [StringLength(100)] public string? ExternalReference { get; set; }
    [StringLength(600)] public string? Note { get; set; }
    [Required] public string IdempotencyKey { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReversePaymentInput
{
    [Range(1, long.MaxValue)] public long TransactionId { get; set; }
    [Required, StringLength(600, MinimumLength = 8, ErrorMessage = "Lý do cần từ 8 đến 600 ký tự.")]
    public string Reason { get; set; } = string.Empty;
    [Required] public string IdempotencyKey { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed record BillingOperationResult(bool Success, string Message, long InvoiceId, string? ReceiptCode = null);

public sealed record InvoiceIssueResult(bool Success, string Message, int PeriodId = 0, int IssuedCount = 0, int SkippedCount = 0);

public sealed class BillingReturnContext
{
    public string? Search { get; set; }
    public int? PeriodId { get; set; }
    public string? Status { get; set; }
    public string? Building { get; set; }
    public decimal? MinBalance { get; set; }
    public decimal? MaxBalance { get; set; }
    public int Page { get; set; } = 1;
}
