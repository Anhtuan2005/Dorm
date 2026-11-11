using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.ViewModels;

public sealed class MaintenanceQueueQuery
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Building { get; set; }
    public int? SelectedId { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed record MaintenanceQueueSummaryViewModel(
    int OpenCount,
    int UrgentCount,
    int UnassignedCount,
    int BreachedSlaCount,
    int BlockedBeds);

public sealed record MaintenanceQueueItemViewModel(
    int Id,
    string Code,
    string Title,
    string RoomCode,
    string BuildingCode,
    string Category,
    string PriorityLabel,
    string PriorityClass,
    string StatusLabel,
    string StatusClass,
    string AgeLabel,
    bool IsSlaBreached,
    bool IsSelected);

public sealed record MaintenanceTimelineItemViewModel(
    string StatusLabel,
    string Note,
    string ActorName,
    DateTime CreatedAtLocal);

public sealed class MaintenanceDetailViewModel
{
    public int Id { get; init; }
    public required string Code { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string RoomCode { get; init; }
    public required string BuildingName { get; init; }
    public int FloorNumber { get; init; }
    public required string ReporterName { get; init; }
    public required string ReporterCode { get; init; }
    public required string Priority { get; init; }
    public required string PriorityLabel { get; init; }
    public required string PriorityClass { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusClass { get; init; }
    public string? AssignedTeam { get; init; }
    public int BlockedBeds { get; init; }
    public int MaxBlockedBeds { get; init; }
    public int Capacity { get; init; }
    public int Occupied { get; init; }
    public int Held { get; init; }
    public int Assignable { get; init; }
    public DateTime CreatedAtLocal { get; init; }
    public DateTime UpdatedAtLocal { get; init; }
    public bool IsSlaBreached { get; init; }
    public bool CanUpdate { get; init; }
    public required string RowVersion { get; init; }
    public required IReadOnlyList<MaintenanceTimelineItemViewModel> Timeline { get; init; }
    public required IReadOnlyList<MaintenanceAttachmentViewModel> Attachments { get; init; }
}

public sealed class MaintenanceQueueViewModel
{
    public required MaintenanceQueueQuery Query { get; init; }
    public required MaintenanceQueueSummaryViewModel Summary { get; init; }
    public required IReadOnlyList<MaintenanceQueueItemViewModel> Items { get; init; }
    public required IReadOnlyList<BillingFilterOption> Buildings { get; init; }
    public required IReadOnlyList<string> Teams { get; init; }
    public MaintenanceDetailViewModel? SelectedRequest { get; init; }
    public int TotalItems { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));
}

public sealed class UpdateMaintenanceInput
{
    [Range(1, int.MaxValue)] public int RequestId { get; set; }
    [Required] public string Status { get; set; } = string.Empty;
    [Required] public string Priority { get; set; } = string.Empty;
    [StringLength(100)] public string? AssignedTeam { get; set; }
    [Range(0, 100)] public int BlockedBeds { get; set; }
    [Required, StringLength(600, MinimumLength = 8, ErrorMessage = "Nội dung cập nhật cần từ 8 đến 600 ký tự.")]
    public string Note { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed record MaintenanceOperationResult(bool Success, string Message, int RequestId = 0);

public sealed class MaintenanceReturnContext
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Building { get; set; }
    public int Page { get; set; } = 1;
}
