using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.ViewModels;

public sealed record StudentIdentityViewModel(
    string FullName,
    string StudentCode,
    string Faculty,
    string PhoneNumber,
    string Email,
    string Initials);

public sealed record StudentStayViewModel(
    string RoomCode,
    string BuildingName,
    int FloorNumber,
    DateTime StartedAt,
    IReadOnlyList<string> RoommateNames,
    int Capacity,
    decimal MonthlyPrice,
    IReadOnlyList<string> Amenities)
{
    public int? BedNumber { get; init; }
}

public sealed record StudentHousingRequestViewModel(
    long Id,
    string Code,
    string RowVersion,
    string TypeLabel,
    string StatusLabel,
    string StatusClass,
    string? TargetRoomCode,
    string StudentReason,
    string? DecisionReason,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string ResponsibilityLabel,
    string NextActionLabel,
    int WaitingHours,
    bool CanCancel,
    bool CanSupplement,
    IReadOnlyList<StudentHousingRequestEventViewModel> Events)
{
    public int? TargetBedNumber { get; init; }
}

public sealed record StudentHousingRequestEventViewModel(
    string StatusLabel,
    string StatusClass,
    string Note,
    string ActorName,
    DateTime CreatedAt);

public sealed record StudentInvoiceLineViewModel(string Description, decimal Amount);

public sealed record StudentChargeChange(string Label, decimal PreviousAmount, decimal CurrentAmount)
{
    public decimal Difference => CurrentAmount - PreviousAmount;
}

public sealed record StudentInvoiceComparison(
    long PreviousInvoiceId,
    string PreviousPeriod,
    decimal Difference,
    IReadOnlyList<StudentChargeChange> Changes);

public sealed record StudentInvoiceTransactionViewModel(
    string Code,
    decimal Amount,
    string MethodLabel,
    DateTime ReceivedAt,
    bool IsReversal,
    string? Note)
{
    public long Id { get; init; }
    public bool HasBeenReversed { get; init; }
}

public sealed record StudentInvoiceViewModel(
    long Id,
    string Code,
    string PeriodName,
    DateOnly DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Balance,
    string StatusLabel,
    string StatusClass,
    bool NeedsPayment,
    IReadOnlyList<StudentInvoiceLineViewModel> Lines,
    IReadOnlyList<StudentInvoiceTransactionViewModel> Transactions)
{
    public StudentInvoiceComparison? Comparison { get; init; }
}

public sealed record StudentRepairUpdateViewModel(
    string StatusLabel,
    string Note,
    DateTime CreatedAt);

public sealed record MaintenanceAttachmentViewModel(long Id, string FileName, string ContentType);

public sealed record StudentRepairViewModel(
    int Id,
    string Code,
    string RoomCode,
    string Title,
    string Category,
    string PriorityLabel,
    string StatusLabel,
    string StatusClass,
    string Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<StudentRepairUpdateViewModel> Updates,
    IReadOnlyList<MaintenanceAttachmentViewModel> Attachments)
{
    public bool ShareWithRoom { get; init; }
}

public sealed record SharedStudentRepair(
    int Id, string Code, string Title, string Category, string Status, string StatusClass,
    DateTime UpdatedAt, bool IsFollowing);

public sealed record StudentNotificationViewModel(
    long Id,
    string Type,
    string Title,
    string Body,
    string TargetUrl,
    DateTime CreatedAt,
    bool IsUnread);

public sealed class StudentNotificationsViewModel
{
    public required IReadOnlyList<StudentNotificationViewModel> Notifications { get; init; }
    public int UnreadCount { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
}

public sealed class StudentDashboardViewModel
{
    public required StudentIdentityViewModel Student { get; init; }
    public StudentStayViewModel? CurrentStay { get; init; }
    public StudentHousingRequestViewModel? ActiveRequest { get; init; }
    public StudentInvoiceViewModel? NextInvoice { get; init; }
    public StudentRepairViewModel? ActiveRepair { get; init; }
    public required IReadOnlyList<StudentNotificationViewModel> Notifications { get; init; }
    public int UnreadNotificationCount => Notifications.Count(x => x.IsUnread);
}

public sealed class StudentRoomSearchQuery
{
    public string? Building { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Amenity { get; set; }
    public string Sort { get; set; } = "availability";
    public int Page { get; set; } = 1;
}

public sealed record StudentRoomOptionViewModel(
    int Id,
    string Code,
    string BuildingName,
    int FloorNumber,
    string RoomType,
    decimal MonthlyPrice,
    int Capacity,
    int Occupied,
    int Assignable,
    IReadOnlyList<string> Amenities,
    bool IsCurrentRoom,
    string MatchReason)
{
    public IReadOnlyList<RoomPhotoViewModel> Photos { get; init; } = [];
}

public sealed record StudentRoomPreviewViewModel(
    StudentRoomOptionViewModel Room,
    IReadOnlyList<int> AvailableBeds,
    bool HasActiveRequest,
    bool IsTransfer)
{
    public bool CanApply => !HasActiveRequest && AvailableBeds.Count > 0;
    public SubmitHousingRequestInput Draft { get; init; } = new();
}

public sealed class StudentRoomSearchViewModel
{
    public required StudentIdentityViewModel Student { get; init; }
    public required StudentRoomSearchQuery Query { get; init; }
    public required IReadOnlyList<StudentRoomOptionViewModel> Rooms { get; init; }
    public required IReadOnlyList<string> Buildings { get; init; }
    public required IReadOnlyList<string> Amenities { get; init; }
    public int TotalMatches { get; init; }
    public int TotalPages { get; init; } = 1;
    public StudentHousingRequestViewModel? ActiveRequest { get; init; }
    public string? CurrentRoomCode { get; init; }
}

public sealed class SubmitHousingRequestInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng.")]
    public int TargetRoomId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn số giường.")]
    public int TargetBedNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng cho biết lý do chuyển phòng.")]
    [StringLength(600, MinimumLength = 10, ErrorMessage = "Lý do cần từ 10 đến 600 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class SupplementHousingRequestInput
{
    [Range(1, long.MaxValue)] public long Id { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập nội dung bổ sung.")]
    [StringLength(600, MinimumLength = 10, ErrorMessage = "Nội dung bổ sung cần từ 10 đến 600 ký tự.")]
    public string Information { get; set; } = string.Empty;
}

public sealed class StudentHousingRequestActionInput
{
    [Range(1, long.MaxValue)] public long Id { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class SubmitCheckoutRequestInput
{
    [Required(ErrorMessage = "Vui lòng cho biết lý do trả phòng.")]
    [StringLength(600, MinimumLength = 10, ErrorMessage = "Lý do cần từ 10 đến 600 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class StudentApplicationsViewModel
{
    public required StudentIdentityViewModel Student { get; init; }
    public required IReadOnlyList<StudentHousingRequestViewModel> Requests { get; init; }
    public string? CurrentRoomCode { get; init; }
    public bool HasActiveRequest { get; init; }
    public bool CanSubmitCheckout => CurrentRoomCode is not null && !HasActiveRequest;
}

public sealed class StudentInvoicesViewModel
{
    public required StudentIdentityViewModel Student { get; init; }
    public required IReadOnlyList<StudentInvoiceViewModel> Invoices { get; init; }
    public decimal OutstandingBalance => Invoices.Sum(x => x.Balance);
}

public sealed class CreateRepairRequestInput
{
    [Required(ErrorMessage = "Vui lòng chọn nhóm sự cố.")]
    [StringLength(60)]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề sự cố.")]
    [StringLength(180, MinimumLength = 5, ErrorMessage = "Tiêu đề cần từ 5 đến 180 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng mô tả tình trạng cần sửa chữa.")]
    [StringLength(1000, MinimumLength = 15, ErrorMessage = "Mô tả cần từ 15 đến 1.000 ký tự.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn mức độ ảnh hưởng.")]
    public string Priority { get; set; } = "Medium";

    public List<IFormFile> Attachments { get; set; } = [];
    public bool ShareWithRoom { get; set; }
}

public sealed class StudentRepairsViewModel
{
    public required StudentIdentityViewModel Student { get; init; }
    public required string CurrentRoomCode { get; init; }
    public bool CanCreateRepair { get; init; }
    public required IReadOnlyList<StudentRepairViewModel> Repairs { get; init; }
    public IReadOnlyList<SharedStudentRepair> SharedRepairs { get; init; } = [];
    public CreateRepairRequestInput NewRequest { get; init; } = new();
}

public sealed record StudentOperationResult(bool Success, string Message);
