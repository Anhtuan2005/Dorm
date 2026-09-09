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
    int Roommates,
    int Capacity,
    decimal MonthlyPrice,
    IReadOnlyList<string> Amenities);

public sealed record StudentHousingRequestViewModel(
    long Id,
    string Code,
    string TypeLabel,
    string StatusLabel,
    string StatusClass,
    string? TargetRoomCode,
    string StudentReason,
    string? DecisionReason,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    bool CanCancel);

public sealed record StudentInvoiceLineViewModel(string Description, decimal Amount);

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
    IReadOnlyList<StudentInvoiceLineViewModel> Lines);

public sealed record StudentRepairUpdateViewModel(
    string StatusLabel,
    string Note,
    DateTime CreatedAt);

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
    IReadOnlyList<StudentRepairUpdateViewModel> Updates);

public sealed record StudentNotificationViewModel(
    long Id,
    string Type,
    string Title,
    string Body,
    string TargetUrl,
    DateTime CreatedAt,
    bool IsUnread);

public sealed class StudentDashboardViewModel
{
    public required StudentIdentityViewModel Student { get; init; }
    public required StudentStayViewModel CurrentStay { get; init; }
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
    string MatchReason);

public sealed class StudentRoomSearchViewModel
{
    public required StudentIdentityViewModel Student { get; init; }
    public required StudentRoomSearchQuery Query { get; init; }
    public required IReadOnlyList<StudentRoomOptionViewModel> Rooms { get; init; }
    public required IReadOnlyList<string> Buildings { get; init; }
    public required IReadOnlyList<string> Amenities { get; init; }
    public StudentHousingRequestViewModel? ActiveRequest { get; init; }
    public string CurrentRoomCode { get; init; } = string.Empty;
}

public sealed class SubmitHousingRequestInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng.")]
    public int TargetRoomId { get; set; }

    [Required(ErrorMessage = "Vui lòng cho biết lý do chuyển phòng.")]
    [StringLength(600, MinimumLength = 10, ErrorMessage = "Lý do cần từ 10 đến 600 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class StudentApplicationsViewModel
{
    public required StudentIdentityViewModel Student { get; init; }
    public required IReadOnlyList<StudentHousingRequestViewModel> Requests { get; init; }
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
}

public sealed class StudentRepairsViewModel
{
    public required StudentIdentityViewModel Student { get; init; }
    public required string CurrentRoomCode { get; init; }
    public required IReadOnlyList<StudentRepairViewModel> Repairs { get; init; }
    public CreateRepairRequestInput NewRequest { get; init; } = new();
}

public sealed record StudentOperationResult(bool Success, string Message);
