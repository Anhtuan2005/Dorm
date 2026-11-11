using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.ViewModels;

public sealed class ApplicationQueueQuery
{
    public string Scope { get; set; } = "attention";
    public string? Search { get; set; }
    public string? Type { get; set; }
    public bool OverdueOnly { get; set; }
    public long? SelectedId { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed record ApplicationQueueSummaryViewModel(
    int Active,
    int New,
    int Overdue,
    int UnderReview,
    int NeedsInformation,
    int CompletedToday);

public sealed record ApplicationQueueItemViewModel(
    long Id,
    string Code,
    string StudentCode,
    string StudentName,
    string Initials,
    string TypeLabel,
    string StatusLabel,
    string StatusClass,
    string? CurrentRoomCode,
    string? TargetRoomCode,
    DateTime SubmittedAt,
    int WaitingHours,
    bool IsOverdue,
    string ResponsibilityLabel,
    string NextActionLabel,
    bool IsSelected);

public sealed record PlacementRoomViewModel(
    string Code,
    string BuildingName,
    int FloorNumber,
    string RoomType,
    int Capacity,
    int Occupied,
    int Held,
    int Blocked,
    int Assignable,
    decimal MonthlyPrice,
    string StateLabel,
    string StateClass);

public sealed record ApplicationAuditEventViewModel(
    string StatusLabel,
    string StatusClass,
    string Note,
    string ActorName,
    DateTime CreatedAt);

public sealed class ApplicationCaseViewModel
{
    public long Id { get; init; }
    public int StudentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
    public string StudentCode { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string Faculty { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusClass { get; init; } = string.Empty;
    public string StudentReason { get; init; } = string.Empty;
    public string? DecisionReason { get; init; }
    public DateTime SubmittedAt { get; init; }
    public int WaitingHours { get; init; }
    public bool IsOverdue { get; init; }
    public string ResponsibilityLabel { get; init; } = string.Empty;
    public string NextActionLabel { get; init; } = string.Empty;
    public PlacementRoomViewModel? CurrentRoom { get; init; }
    public PlacementRoomViewModel? TargetRoom { get; init; }
    public decimal OutstandingBalance { get; init; }
    public int OpenRepairCount { get; init; }
    public bool CanStartReview { get; init; }
    public bool CanDecide { get; init; }
    public bool CanRequestInformation { get; init; }
    public bool CanReject { get; init; }
    public bool CanApprove { get; init; }
    public bool CanConfirmMoveIn { get; init; }
    public DateTime? HoldExpiresAt { get; init; }
    public bool IsCheckout => RequestType == "Checkout";
    public string PrimaryActionLabel => IsCheckout ? "Duyệt trả phòng" : "Duyệt và giữ chỗ";
    public string? ApprovalBlockReason { get; init; }
    public IReadOnlyList<ApplicationAuditEventViewModel> Events { get; init; } = [];
}

public sealed class ApplicationQueueViewModel
{
    public required ApplicationQueueQuery Query { get; init; }
    public required ApplicationQueueSummaryViewModel Summary { get; init; }
    public required IReadOnlyList<ApplicationQueueItemViewModel> Items { get; init; }
    public ApplicationCaseViewModel? SelectedCase { get; init; }
    public int TotalItems { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));
}

public sealed class ApplicationActionInput
{
    [Range(1, long.MaxValue)] public long Id { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(600)] public string? Reason { get; set; }
}

public sealed record ApplicationManagementResult(bool Success, string Message, long? RequestId = null);

public sealed class StudentDirectoryQuery
{
    public string? Search { get; set; }
    public string? Building { get; set; }
    public string Account { get; set; } = "all";
    public int? SelectedId { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed record StudentDirectoryItemViewModel(
    int Id,
    string StudentCode,
    string FullName,
    string Initials,
    string Faculty,
    string? RoomCode,
    string? Email,
    bool HasAccount,
    bool IsLocked,
    bool IsSelected);

public sealed class StudentDirectoryDetailViewModel
{
    public int Id { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string Faculty { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? RoomCode { get; init; }
    public DateTime? StayStartedAt { get; init; }
    public string? Email { get; init; }
    public bool HasAccount { get; init; }
    public bool IsLocked { get; init; }
    public int RequestCount { get; init; }
    public decimal OutstandingBalance { get; init; }
    public required IReadOnlyList<StudentStayHistoryViewModel> StayHistory { get; init; }
}

public sealed record StudentStayHistoryViewModel(string RoomCode, string BuildingName, DateTime StartedAt, DateTime? EndedAt, bool IsCurrent);

public sealed class StudentDirectoryViewModel
{
    public required StudentDirectoryQuery Query { get; init; }
    public required IReadOnlyList<StudentDirectoryItemViewModel> Items { get; init; }
    public required IReadOnlyList<string> Buildings { get; init; }
    public StudentDirectoryDetailViewModel? SelectedStudent { get; init; }
    public int LinkedAccountCount { get; init; }
    public int UnlinkedAccountCount { get; init; }
    public int TotalItems { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));
}

public class CreateStudentProfileInput
{
    [Required, StringLength(20, MinimumLength = 4)] public string StudentCode { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = string.Empty;
    [Required, StringLength(16)] public string Gender { get; set; } = string.Empty;
    [StringLength(100)] public string Faculty { get; set; } = string.Empty;
    [Phone, StringLength(20)] public string PhoneNumber { get; set; } = string.Empty;
}

public sealed class UpdateStudentProfileInput : CreateStudentProfileInput
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
}

public sealed class CreateStudentAccountInput
{
    [Range(1, int.MaxValue)] public int StudentId { get; set; }
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)] public string TemporaryPassword { get; set; } = string.Empty;
}

public sealed class ResetStudentPasswordInput
{
    [Range(1, int.MaxValue)] public int StudentId { get; set; }
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)] public string TemporaryPassword { get; set; } = string.Empty;
}

public sealed record StudentManagementResult(bool Success, string Message, int? StudentId = null);
