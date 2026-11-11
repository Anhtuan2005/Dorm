using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.ViewModels;

public sealed class RoomMapQuery
{
    public string Building { get; set; } = "A";
    public string? Room { get; set; } = "A-302";
    public int? Floor { get; set; }
    public string? RoomType { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Status { get; set; }
    public bool OnlyAvailable { get; set; }
    public bool ShowDetail { get; set; }
    public string? Search { get; set; }
}

public sealed record CapacityViewModel(
    int Capacity,
    int Occupied,
    int Held,
    int Assignable,
    int Blocked);

public sealed record BuildingSummaryViewModel(
    string Code,
    string Name,
    CapacityViewModel Capacity);

public sealed record RoomCardViewModel(
    int Id,
    string Code,
    int FloorNumber,
    string RoomType,
    decimal MonthlyPrice,
    int Capacity,
    int Occupied,
    int Held,
    int Assignable,
    int Blocked,
    string StatusLabel,
    string StatusClass,
    bool IsSelected);

public sealed record FloorMapViewModel(
    int Number,
    IReadOnlyList<RoomCardViewModel> Rooms);

public sealed record StudentSummaryViewModel(string StudentCode, string FullName);

public sealed record IncidentSummaryViewModel(
    string Code,
    string Title,
    string PriorityLabel,
    string StatusLabel);

public sealed record RoomDetailViewModel(
    string Code,
    string RoomType,
    decimal MonthlyPrice,
    CapacityViewModel Capacity,
    string StatusLabel,
    string StatusClass,
    IReadOnlyList<StudentSummaryViewModel> Students,
    IReadOnlyList<string> Amenities,
    IReadOnlyList<IncidentSummaryViewModel> Incidents);

public sealed class RoomMapViewModel
{
    public required RoomMapQuery Query { get; init; }
    public required CapacityViewModel GlobalCapacity { get; init; }
    public required CapacityViewModel SelectedBuildingCapacity { get; init; }
    public required string SelectedBuildingName { get; init; }
    public required IReadOnlyList<BuildingSummaryViewModel> Buildings { get; init; }
    public required IReadOnlyList<FloorMapViewModel> Floors { get; init; }
    public required IReadOnlyList<string> RoomTypes { get; init; }
    public RoomDetailViewModel? SelectedRoom { get; init; }
    public int OverdueApplicationCount { get; init; }
    public int OverdueInvoiceCount { get; init; }
    public decimal OverdueInvoiceAmount { get; init; }
    public int UrgentMaintenanceCount { get; init; }
    public int VisibleRoomCount => Floors.Sum(x => x.Rooms.Count);
}

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại."), DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới."), DataType(DataType.Password), StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới cần ít nhất 8 ký tự.")]
    public string NewPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu chưa khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
