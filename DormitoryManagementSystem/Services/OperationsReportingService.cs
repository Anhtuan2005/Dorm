using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class OperationsReportingService(
    DormitoryDbContext db,
    TimeProvider timeProvider) : IOperationsReportingService
{
    private static readonly HousingRequestStatus[] ActiveRequestStatuses = HousingRequestWorkflow.ActiveStatuses;
    private static readonly MaintenanceStatus[] OpenMaintenanceStatuses =
        [MaintenanceStatus.New, MaintenanceStatus.Assigned, MaintenanceStatus.InProgress, MaintenanceStatus.WaitingParts];

    public async Task<ManagerDashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var businessDate = BusinessDate(now);
        var monthStartUtc = ToUtc(new DateOnly(businessDate.Year, businessDate.Month, 1));
        var nextMonthUtc = ToUtc(new DateOnly(businessDate.Year, businessDate.Month, 1).AddMonths(1));

        var buildings = await LoadBuildingMetricsAsync(null, null, cancellationToken);
        var capacity = SumCapacity(buildings);
        var invoiceRows = await InvoiceMetrics().ToListAsync(cancellationToken);
        var openInvoices = invoiceRows.Where(x => !x.Cancelled && x.Total > x.Collected).ToList();
        var collectedThisMonth = await db.PaymentTransactions.AsNoTracking()
            .Where(x => x.RecordedAt >= monthStartUtc && x.RecordedAt < nextMonthUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var overdueAt = now.AddHours(-HousingRequestWorkflow.ManagerSlaHours);

        var requestSummary = await db.HousingRequests.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Active = group.Count(x => ActiveRequestStatuses.Contains(x.Status)),
                Overdue = group.Count(x =>
                    (x.Status == HousingRequestStatus.Submitted || x.Status == HousingRequestStatus.UnderReview) &&
                    (x.ActionRequiredAt ?? x.SubmittedAt) <= overdueAt),
                NeedsInformation = group.Count(x => x.Status == HousingRequestStatus.NeedsInformation)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var maintenanceSummary = await db.MaintenanceRequests.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Open = group.Count(x => OpenMaintenanceStatuses.Contains(x.Status)),
                Urgent = group.Count(x => OpenMaintenanceStatuses.Contains(x.Status) && x.Priority == MaintenancePriority.Urgent)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var recentActivities = await LoadRecentActivitiesAsync(cancellationToken);
        return new ManagerDashboardViewModel
        {
            GeneratedAt = now,
            Capacity = capacity,
            ActiveStudents = capacity.Occupied,
            ActiveApplications = requestSummary?.Active ?? 0,
            OverdueApplications = requestSummary?.Overdue ?? 0,
            NeedsInformation = requestSummary?.NeedsInformation ?? 0,
            OutstandingInvoices = openInvoices.Count,
            OverdueInvoices = openInvoices.Count(x => x.DueDate < businessDate),
            OutstandingBalance = openInvoices.Sum(x => x.Total - x.Collected),
            CollectedThisMonth = collectedThisMonth,
            OpenMaintenance = maintenanceSummary?.Open ?? 0,
            UrgentMaintenance = maintenanceSummary?.Urgent ?? 0,
            Buildings = buildings,
            RecentActivities = recentActivities
        };
    }

    public async Task<OperationsReportViewModel> GetReportAsync(
        OperationsReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var businessDate = BusinessDate(now);
        var periods = await db.BillingPeriods.AsNoTracking()
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);
        var selectedPeriod = periods.FirstOrDefault(x => x.Id == query.PeriodId)
            ?? periods.FirstOrDefault(x => x.StartDate <= businessDate && x.EndDate >= businessDate)
            ?? periods.FirstOrDefault()
            ?? throw new InvalidOperationException("Chưa có kỳ thu để lập báo cáo.");

        var buildingOptions = await db.Buildings.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new ReportFilterOption(x.Code, x.Name))
            .ToListAsync(cancellationToken);
        var buildingCode = buildingOptions.Any(x => x.Value == query.Building) ? query.Building : null;
        query.PeriodId = selectedPeriod.Id;
        query.Building = buildingCode;

        var startUtc = ToUtc(selectedPeriod.StartDate);
        var endUtc = ToUtc(selectedPeriod.EndDate.AddDays(1));
        var financeRows = await InvoiceMetrics(periodId: selectedPeriod.Id, buildingCode: buildingCode)
            .ToListAsync(cancellationToken);
        var activeFinanceRows = financeRows.Where(x => !x.Cancelled).ToList();
        var invoiced = activeFinanceRows.Sum(x => x.Total);
        var collected = activeFinanceRows.Sum(x => x.Collected);
        var outstanding = activeFinanceRows.Sum(x => Math.Max(0m, x.Total - x.Collected));
        var overdueRows = activeFinanceRows.Where(x => x.DueDate < businessDate && x.Total > x.Collected).ToList();

        var requestRows = await db.HousingRequests.AsNoTracking()
            .Where(x => x.SubmittedAt >= startUtc && x.SubmittedAt < endUtc)
            .Where(x => buildingCode == null ||
                (x.TargetRoom != null && x.TargetRoom.Floor.Building.Code == buildingCode) ||
                (x.TargetRoom == null && x.SourceStay != null && x.SourceStay.Room.Floor.Building.Code == buildingCode))
            .Select(x => new { x.Status, x.Type })
            .ToListAsync(cancellationToken);
        var resolvedRequests = requestRows.Count(x => x.Status is HousingRequestStatus.Approved or HousingRequestStatus.Rejected);

        var maintenanceRows = await db.MaintenanceRequests.AsNoTracking()
            .Where(x => x.CreatedAt < endUtc && (buildingCode == null || x.Room.Floor.Building.Code == buildingCode))
            .Select(x => new { x.CreatedAt, x.Status, x.Priority, x.BlockedBeds })
            .ToListAsync(cancellationToken);
        var createdMaintenance = maintenanceRows.Where(x => x.CreatedAt >= startUtc).ToList();
        var openMaintenance = maintenanceRows.Where(x => OpenMaintenanceStatuses.Contains(x.Status)).ToList();
        var resolvedMaintenance = createdMaintenance.Count(x => x.Status is MaintenanceStatus.Resolved or MaintenanceStatus.Closed);

        var buildings = await LoadBuildingMetricsAsync(buildingCode, selectedPeriod.Id, cancellationToken);
        var trends = await LoadTrendsAsync(periods.Take(6).OrderBy(x => x.StartDate).ToList(), buildingCode, cancellationToken);
        return new OperationsReportViewModel
        {
            Query = query,
            GeneratedAt = now,
            PeriodName = selectedPeriod.Name,
            PeriodStart = selectedPeriod.StartDate,
            PeriodEnd = selectedPeriod.EndDate,
            Capacity = SumCapacity(buildings),
            Finance = new FinanceReportViewModel(
                invoiced,
                collected,
                outstanding,
                overdueRows.Sum(x => x.Total - x.Collected),
                activeFinanceRows.Count,
                overdueRows.Count,
                OperationsMetricsPolicy.Percentage(collected, invoiced)),
            Workflow = new WorkflowReportViewModel(
                requestRows.Count,
                requestRows.Count(x => x.Status == HousingRequestStatus.Approved),
                requestRows.Count(x => x.Status == HousingRequestStatus.Rejected),
                requestRows.Count(x => x.Status == HousingRequestStatus.Cancelled),
                requestRows.Count(x => x.Status == HousingRequestStatus.NeedsInformation),
                requestRows.Count(x => x.Type == HousingRequestType.Registration),
                requestRows.Count(x => x.Type == HousingRequestType.Transfer),
                requestRows.Count(x => x.Type == HousingRequestType.Checkout),
                OperationsMetricsPolicy.Percentage(requestRows.Count(x => x.Status == HousingRequestStatus.Approved), resolvedRequests)),
            Maintenance = new MaintenanceReportViewModel(
                createdMaintenance.Count,
                openMaintenance.Count,
                openMaintenance.Count(x => x.Priority == MaintenancePriority.Urgent),
                resolvedMaintenance,
                openMaintenance.Sum(x => x.BlockedBeds),
                OperationsMetricsPolicy.Percentage(resolvedMaintenance, createdMaintenance.Count)),
            Buildings = buildings,
            Trends = trends,
            Periods = periods.Select(x => new ReportFilterOption(x.Id.ToString(), x.Name)).ToList(),
            BuildingOptions = buildingOptions
        };
    }

    public async Task<ManagerSettingsViewModel> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var migrations = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        return new ManagerSettingsViewModel
        {
            SiteName = await db.Buildings.AsNoTracking().OrderBy(x => x.SortOrder).Select(x => x.SiteName).FirstOrDefaultAsync(cancellationToken)
                ?? "Chưa cấu hình",
            BusinessTimeZone = "Asia/Saigon (UTC+07:00)",
            ManagerSlaHours = HousingRequestWorkflow.ManagerSlaHours,
            BuildingCount = await db.Buildings.CountAsync(cancellationToken),
            RoomCount = await db.Rooms.CountAsync(cancellationToken),
            BillingPeriodCount = await db.BillingPeriods.CountAsync(cancellationToken),
            DatabaseConnected = await db.Database.CanConnectAsync(cancellationToken),
            LatestMigration = migrations.LastOrDefault() ?? "Chưa có migration"
        };
    }

    private async Task<List<BuildingReportViewModel>> LoadBuildingMetricsAsync(
        string? buildingCode,
        int? periodId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var roomRows = await db.Rooms.AsNoTracking()
            .Where(x => buildingCode == null || x.Floor.Building.Code == buildingCode)
            .Select(x => new RoomMetricRow(
                x.Floor.Building.Code,
                x.Floor.Building.Name,
                x.Floor.Building.SortOrder,
                x.Capacity,
                x.Stays.Count(stay => stay.EndedAt == null),
                x.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now).Sum(hold => (int?)hold.HeldBeds) ?? 0,
                x.MaintenanceRequests.Where(item => OpenMaintenanceStatuses.Contains(item.Status)).Sum(item => (int?)item.BlockedBeds) ?? 0,
                x.MaintenanceRequests.Count(item => OpenMaintenanceStatuses.Contains(item.Status))))
            .ToListAsync(cancellationToken);

        var invoiceRows = await InvoiceMetrics(periodId: periodId, buildingCode: buildingCode, activeOnly: true)
            .ToListAsync(cancellationToken);
        var outstandingByBuilding = invoiceRows
            .GroupBy(x => x.BuildingCode)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(x => x.Total > x.Collected ? x.Total - x.Collected : 0m));

        return roomRows
            .GroupBy(x => new { x.BuildingCode, x.BuildingName, x.SortOrder })
            .OrderBy(group => group.Key.SortOrder)
            .Select(group =>
            {
                var roomCapacities = group.Select(x => RoomCapacityPolicy.Calculate(x.Capacity, x.Occupied, x.Held, x.Blocked)).ToList();
                var capacity = ToCapacity(
                    roomCapacities.Sum(x => x.Capacity),
                    roomCapacities.Sum(x => x.Occupied),
                    roomCapacities.Sum(x => x.Held),
                    roomCapacities.Sum(x => x.Blocked),
                    roomCapacities.Sum(x => x.Assignable));
                return new BuildingReportViewModel(
                    group.Key.BuildingCode,
                    group.Key.BuildingName,
                    capacity,
                    group.Sum(x => x.ActiveIncidents),
                    outstandingByBuilding.GetValueOrDefault(group.Key.BuildingCode));
            })
            .ToList();
    }

    private async Task<List<PeriodTrendViewModel>> LoadTrendsAsync(
        IReadOnlyList<BillingPeriod> periods,
        string? buildingCode,
        CancellationToken cancellationToken)
    {
        if (periods.Count == 0) return [];
        var periodIds = periods.Select(x => x.Id).ToArray();
        var rows = await InvoiceMetrics(periodIds: periodIds, buildingCode: buildingCode, activeOnly: true)
            .ToListAsync(cancellationToken);
        return periods.Select(period =>
        {
            var periodRows = rows.Where(x => x.PeriodId == period.Id).ToList();
            var invoiced = periodRows.Sum(x => x.Total);
            var collected = periodRows.Sum(x => x.Collected);
            return new PeriodTrendViewModel(
                period.Name.Replace("Tháng ", string.Empty),
                invoiced,
                collected,
                periodRows.Sum(x => Math.Max(0m, x.Total - x.Collected)),
                OperationsMetricsPolicy.Percentage(collected, invoiced));
        }).ToList();
    }

    private async Task<List<OperationsActivityViewModel>> LoadRecentActivitiesAsync(CancellationToken cancellationToken)
    {
        var applications = await db.HousingRequestEvents.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).Take(5)
            .Select(x => new OperationsActivityViewModel(
                "application", "Đơn lưu trú", x.HousingRequest.Code,
                x.Note, x.CreatedAt, $"/Applications/Index?Scope=all&SelectedId={x.HousingRequestId}"))
            .ToListAsync(cancellationToken);
        var paymentRows = await db.PaymentTransactions.AsNoTracking()
            .OrderByDescending(x => x.RecordedAt).Take(5)
            .Select(x => new { x.Code, x.Amount, x.RecordedAt, x.InvoiceId })
            .ToListAsync(cancellationToken);
        var payments = paymentRows.Select(x => new OperationsActivityViewModel(
            "billing", "Thu phí", x.Code,
            x.Amount >= 0 ? $"Đã ghi nhận {x.Amount:N0} đ" : $"Đã hoàn tác {-x.Amount:N0} đ",
            x.RecordedAt, $"/Billing/Index?SelectedId={x.InvoiceId}"));
        var maintenance = await db.MaintenanceUpdates.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).Take(5)
            .Select(x => new OperationsActivityViewModel(
                "maintenance", "Sự cố", x.MaintenanceRequest.Code,
                x.Note, x.CreatedAt, $"/Maintenance/Index?SelectedId={x.MaintenanceRequestId}"))
            .ToListAsync(cancellationToken);
        return applications.Concat(payments).Concat(maintenance)
            .OrderByDescending(x => x.OccurredAt)
            .Take(8)
            .ToList();
    }

    private IQueryable<InvoiceMetricRow> InvoiceMetrics(
        int? periodId = null,
        IReadOnlyCollection<int>? periodIds = null,
        string? buildingCode = null,
        bool activeOnly = false)
    {
        var source = db.Invoices.AsNoTracking().AsQueryable();
        if (periodId.HasValue)
            source = source.Where(x => x.BillingPeriodId == periodId.Value);
        if (periodIds is not null)
            source = source.Where(x => periodIds.Contains(x.BillingPeriodId));
        if (!string.IsNullOrWhiteSpace(buildingCode))
            source = source.Where(x => x.Stay != null && x.Stay.Room.Floor.Building.Code == buildingCode);
        if (activeOnly)
            source = source.Where(x => x.Status == InvoiceStatus.Issued);

        return source.Select(x => new InvoiceMetricRow(
            x.BillingPeriodId,
            x.Stay != null ? x.Stay.Room.Floor.Building.Code : "—",
            x.TotalAmount,
            x.Transactions.Sum(transaction => (decimal?)transaction.Amount) ?? 0m,
            x.BillingPeriod.DueDate,
            x.Status == InvoiceStatus.Cancelled));
    }

    private static CapacityReportViewModel SumCapacity(IEnumerable<BuildingReportViewModel> buildings)
    {
        var rows = buildings.Select(x => x.Capacity).ToList();
        return ToCapacity(
            rows.Sum(x => x.Capacity), rows.Sum(x => x.Occupied), rows.Sum(x => x.Held),
            rows.Sum(x => x.Blocked), rows.Sum(x => x.Assignable));
    }

    private static CapacityReportViewModel ToCapacity(int capacity, int occupied, int held, int blocked, int assignable) =>
        new(capacity, occupied, held, blocked, assignable, OperationsMetricsPolicy.Percentage(occupied, capacity));

    private static DateOnly BusinessDate(DateTime nowUtc) => DateOnly.FromDateTime(nowUtc.AddHours(7));
    private static DateTime ToUtc(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);

    private sealed record RoomMetricRow(
        string BuildingCode,
        string BuildingName,
        int SortOrder,
        int Capacity,
        int Occupied,
        int Held,
        int Blocked,
        int ActiveIncidents);

    private sealed record InvoiceMetricRow(
        int PeriodId,
        string BuildingCode,
        decimal Total,
        decimal Collected,
        DateOnly DueDate,
        bool Cancelled);
}
