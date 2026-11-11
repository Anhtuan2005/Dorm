using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.ViewModels;

public sealed class OperationsReportQuery
{
    public int? PeriodId { get; set; }
    [StringLength(4)] public string? Building { get; set; }
}

public sealed record ReportFilterOption(string Value, string Label);

public sealed record CapacityReportViewModel(
    int Capacity,
    int Occupied,
    int Held,
    int Blocked,
    int Assignable,
    decimal OccupancyRate);

public sealed record BuildingReportViewModel(
    string Code,
    string Name,
    CapacityReportViewModel Capacity,
    int ActiveIncidents,
    decimal OutstandingBalance);

public sealed record OperationsActivityViewModel(
    string Type,
    string TypeLabel,
    string Title,
    string Detail,
    DateTime OccurredAt,
    string TargetUrl);

public sealed record FinanceReportViewModel(
    decimal Invoiced,
    decimal Collected,
    decimal Outstanding,
    decimal Overdue,
    int InvoiceCount,
    int OverdueCount,
    decimal CollectionRate);

public sealed record WorkflowReportViewModel(
    int Submitted,
    int Approved,
    int Rejected,
    int Cancelled,
    int NeedsInformation,
    int Registration,
    int Transfer,
    int Checkout,
    decimal ApprovalRate);

public sealed record MaintenanceReportViewModel(
    int Created,
    int Open,
    int Urgent,
    int Resolved,
    int BlockedBeds,
    decimal ResolutionRate);

public sealed record PeriodTrendViewModel(
    string Label,
    decimal Invoiced,
    decimal Collected,
    decimal Outstanding,
    decimal CollectionRate);

public sealed class ManagerDashboardViewModel
{
    public DateTime GeneratedAt { get; init; }
    public required CapacityReportViewModel Capacity { get; init; }
    public int ActiveStudents { get; init; }
    public int ActiveApplications { get; init; }
    public int OverdueApplications { get; init; }
    public int NeedsInformation { get; init; }
    public int OutstandingInvoices { get; init; }
    public int OverdueInvoices { get; init; }
    public decimal OutstandingBalance { get; init; }
    public decimal CollectedThisMonth { get; init; }
    public int OpenMaintenance { get; init; }
    public int UrgentMaintenance { get; init; }
    public required IReadOnlyList<BuildingReportViewModel> Buildings { get; init; }
    public required IReadOnlyList<OperationsActivityViewModel> RecentActivities { get; init; }
}

public sealed class OperationsReportViewModel
{
    public required OperationsReportQuery Query { get; init; }
    public DateTime GeneratedAt { get; init; }
    public required string PeriodName { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public required CapacityReportViewModel Capacity { get; init; }
    public required FinanceReportViewModel Finance { get; init; }
    public required WorkflowReportViewModel Workflow { get; init; }
    public required MaintenanceReportViewModel Maintenance { get; init; }
    public required IReadOnlyList<BuildingReportViewModel> Buildings { get; init; }
    public required IReadOnlyList<PeriodTrendViewModel> Trends { get; init; }
    public required IReadOnlyList<ReportFilterOption> Periods { get; init; }
    public required IReadOnlyList<ReportFilterOption> BuildingOptions { get; init; }
}

public sealed class ManagerSettingsViewModel
{
    public required string SiteName { get; init; }
    public required string BusinessTimeZone { get; init; }
    public int ManagerSlaHours { get; init; }
    public int BuildingCount { get; init; }
    public int RoomCount { get; init; }
    public int BillingPeriodCount { get; init; }
    public bool DatabaseConnected { get; init; }
    public required string LatestMigration { get; init; }
}
