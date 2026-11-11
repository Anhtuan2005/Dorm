using System.Data;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class MaintenanceManagementService(
    DormitoryDbContext db,
    TimeProvider timeProvider) : IMaintenanceManagementService
{
    private const int PageSize = 24;
    private static readonly MaintenanceStatus[] OpenStatuses =
        [MaintenanceStatus.New, MaintenanceStatus.Assigned, MaintenanceStatus.InProgress, MaintenanceStatus.WaitingParts];
    private static readonly string[] Teams = ["Tổ Điện nước", "Tổ Thiết bị", "Tổ Hạ tầng", "Tổ Vệ sinh", "Trực ban cơ sở"];
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    public async Task<MaintenanceQueueViewModel> GetQueueAsync(
        MaintenanceQueueQuery query,
        CancellationToken cancellationToken = default)
    {
        query.Page = Math.Max(1, query.Page);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var projections = Projections();

        var summaryRows = await projections
            .Select(x => new { x.Status, x.Priority, x.AssignedTeam, x.BlockedBeds, x.CreatedAt })
            .ToListAsync(cancellationToken);
        var openRows = summaryRows.Where(x => IsOpen(x.Status)).ToList();
        var summary = new MaintenanceQueueSummaryViewModel(
            openRows.Count,
            openRows.Count(x => x.Priority == MaintenancePriority.Urgent),
            openRows.Count(x => string.IsNullOrWhiteSpace(x.AssignedTeam)),
            openRows.Count(x => IsSlaBreached(x.Priority, x.CreatedAt, now)),
            openRows.Sum(x => x.BlockedBeds));

        var filtered = ApplyFilters(projections, query, now);
        var totalItems = await filtered.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / PageSize));
        query.Page = Math.Min(query.Page, totalPages);

        var rows = await filtered
            .OrderBy(x => x.Status == MaintenanceStatus.New || x.Status == MaintenanceStatus.Assigned ||
                          x.Status == MaintenanceStatus.InProgress || x.Status == MaintenanceStatus.WaitingParts ? 0 : 1)
            .ThenBy(x => x.Priority == MaintenancePriority.Urgent ? 0
                : x.Priority == MaintenancePriority.High ? 1
                : x.Priority == MaintenancePriority.Medium ? 2 : 3)
            .ThenBy(x => x.CreatedAt)
            .Skip((query.Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var requestedSelectedId = query.SelectedId;
        var selectedId = requestedSelectedId.HasValue && rows.Any(x => x.Id == requestedSelectedId.Value)
            ? requestedSelectedId
            : rows.FirstOrDefault()?.Id;
        query.SelectedId = selectedId;

        var items = rows.Select(x =>
        {
            var priority = PriorityPresentation(x.Priority);
            var status = StatusPresentation(x.Status);
            return new MaintenanceQueueItemViewModel(
                x.Id,
                x.Code,
                x.Title,
                x.RoomCode,
                x.BuildingCode,
                string.IsNullOrWhiteSpace(x.Category) ? "Chưa phân loại" : x.Category,
                priority.Label,
                priority.Css,
                status.Label,
                status.Css,
                AgeLabel(x.UpdatedAt, now),
                IsOpen(x.Status) && IsSlaBreached(x.Priority, x.CreatedAt, now),
                x.Id == selectedId);
        }).ToList();

        var buildings = await db.Buildings.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new BillingFilterOption(x.Code, x.Name))
            .ToListAsync(cancellationToken);

        return new MaintenanceQueueViewModel
        {
            Query = query,
            Summary = summary,
            Items = items,
            Buildings = buildings,
            Teams = Teams,
            SelectedRequest = selectedId.HasValue
                ? await LoadDetailAsync(selectedId.Value, now, cancellationToken)
                : null,
            TotalItems = totalItems,
            PageSize = PageSize
        };
    }

    public Task<int> GetAttentionCountAsync(CancellationToken cancellationToken = default) =>
        db.MaintenanceRequests.AsNoTracking().CountAsync(
            x => OpenStatuses.Contains(x.Status) &&
                 (x.Priority == MaintenancePriority.Urgent || x.Priority == MaintenancePriority.High),
            cancellationToken);

    public async Task<MaintenanceOperationResult> UpdateAsync(
        UpdateMaintenanceInput input,
        string managerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MaintenanceStatus>(input.Status, true, out var nextStatus) || !Enum.IsDefined(nextStatus))
            return new(false, "Trạng thái sự cố không hợp lệ.", input.RequestId);
        if (!Enum.TryParse<MaintenancePriority>(input.Priority, true, out var nextPriority) || !Enum.IsDefined(nextPriority))
            return new(false, "Mức ưu tiên không hợp lệ.", input.RequestId);
        if (string.IsNullOrWhiteSpace(input.Note) || input.Note.Trim().Length < 8)
            return new(false, "Nội dung cập nhật cần ít nhất 8 ký tự.", input.RequestId);

        var assignedTeam = string.IsNullOrWhiteSpace(input.AssignedTeam) ? null : input.AssignedTeam.Trim();
        if (assignedTeam is not null && !Teams.Contains(assignedTeam, StringComparer.Ordinal))
            return new(false, "Tổ phụ trách không nằm trong danh sách vận hành.", input.RequestId);
        if (nextStatus is MaintenanceStatus.Assigned or MaintenanceStatus.InProgress or MaintenanceStatus.WaitingParts && assignedTeam is null)
            return new(false, "Vui lòng chọn tổ phụ trách trước khi chuyển trạng thái.", input.RequestId);
        if (input.BlockedBeds < 0)
            return new(false, "Số chỗ tạm khóa không thể âm.", input.RequestId);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => UpdateWithinTransactionAsync(
            input, nextStatus, nextPriority, assignedTeam, managerUserId, cancellationToken));
    }

    private async Task<MaintenanceOperationResult> UpdateWithinTransactionAsync(
        UpdateMaintenanceInput input,
        MaintenanceStatus nextStatus,
        MaintenancePriority nextPriority,
        string? assignedTeam,
        string managerUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var request = await db.MaintenanceRequests
                .Include(x => x.ReporterStudent).ThenInclude(x => x!.User)
                .Include(x => x.Room).ThenInclude(x => x.Stays.Where(stay => stay.EndedAt == null))
                .Include(x => x.Room).ThenInclude(x => x.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now))
                .Include(x => x.Room).ThenInclude(x => x.MaintenanceRequests.Where(item => OpenStatuses.Contains(item.Status)))
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.Id == input.RequestId, cancellationToken);
            if (request is null) return new(false, "Không tìm thấy sự cố.", input.RequestId);
            if (!TrySetOriginalVersion(request, input.RowVersion))
                return new(false, "Sự cố đã thay đổi. Hãy tải lại trước khi cập nhật.", request.Id);
            if (!CanTransition(request.Status, nextStatus))
                return new(false, $"Không thể chuyển từ {StatusPresentation(request.Status).Label} sang {StatusPresentation(nextStatus).Label}.", request.Id);

            var completed = nextStatus is MaintenanceStatus.Resolved or MaintenanceStatus.Closed;
            var blockedBeds = completed ? 0 : input.BlockedBeds;
            var capacityDecision = RoomCapacityPolicy.CanSetBlockedBeds(request.Room, request.Id, blockedBeds, now);
            if (!capacityDecision.Allowed)
                return new(false, capacityDecision.ErrorMessage, request.Id);
            request.Status = nextStatus;
            request.Priority = nextPriority;
            request.AssignedTeam = assignedTeam;
            request.BlockedBeds = blockedBeds;
            request.UpdatedAt = now;
            request.Updates.Add(new MaintenanceUpdate
            {
                Status = nextStatus,
                Note = input.Note.Trim(),
                ActorUserId = managerUserId,
                CreatedAt = now
            });

            if (request.ReporterStudent?.UserId is string reporterUserId)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = reporterUserId,
                    Type = "Maintenance",
                    Title = $"{request.Code} · {StatusPresentation(nextStatus).Label}",
                    Body = input.Note.Trim().Length <= 300 ? input.Note.Trim() : input.Note.Trim()[..300],
                    TargetUrl = "/Student/Repairs",
                    CreatedAt = now
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, $"Đã cập nhật {request.Code} sang {StatusPresentation(nextStatus).Label.ToLowerInvariant()}.", request.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Sự cố vừa được người khác cập nhật. Hãy tải lại và kiểm tra tiến độ.", input.RequestId);
        }
        catch (FormatException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Phiên bản dữ liệu không hợp lệ. Hãy tải lại trang.", input.RequestId);
        }
    }

    private async Task<MaintenanceDetailViewModel?> LoadDetailAsync(
        int id,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var request = await db.MaintenanceRequests.AsNoTracking()
            .Include(x => x.ReporterStudent)
            .Include(x => x.Room).ThenInclude(x => x.Floor).ThenInclude(x => x.Building)
            .Include(x => x.Room).ThenInclude(x => x.Stays.Where(stay => stay.EndedAt == null))
            .Include(x => x.Room).ThenInclude(x => x.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now))
            .Include(x => x.Updates).ThenInclude(x => x.ActorUser)
            .Include(x => x.Attachments)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return null;

        var blocked = await db.MaintenanceRequests.AsNoTracking()
            .Where(x => x.RoomId == request.RoomId &&
                (x.Status == MaintenanceStatus.New ||
                 x.Status == MaintenanceStatus.Assigned ||
                 x.Status == MaintenanceStatus.InProgress ||
                 x.Status == MaintenanceStatus.WaitingParts))
            .SumAsync(x => x.BlockedBeds, cancellationToken);
        var occupied = request.Room.Stays.Count;
        var held = request.Room.Holds.Sum(x => x.HeldBeds);
        var capacity = RoomCapacityPolicy.Calculate(request.Room.Capacity, occupied, held, blocked);
        var otherBlocked = Math.Max(0, blocked - (IsOpen(request.Status) ? request.BlockedBeds : 0));
        var maxBlocked = RoomCapacityPolicy.MaximumBlockedBeds(request.Room.Capacity, capacity.Occupied, held, otherBlocked);
        var priority = PriorityPresentation(request.Priority);
        var status = StatusPresentation(request.Status);
        var timeline = request.Updates.OrderByDescending(x => x.CreatedAt)
            .Select(x => new MaintenanceTimelineItemViewModel(
                StatusPresentation(x.Status).Label,
                x.Note,
                x.ActorUser?.FullName ?? (request.ReporterStudent is not null ? request.ReporterStudent.FullName : "Hệ thống"),
                ToBusinessLocal(x.CreatedAt)))
            .ToList();
        if (timeline.Count == 0)
            timeline.Add(new(status.Label, "Sự cố đã được tiếp nhận.", request.ReporterStudent?.FullName ?? "Ban quản lý", ToBusinessLocal(request.CreatedAt)));

        return new MaintenanceDetailViewModel
        {
            Id = request.Id,
            Code = request.Code,
            Title = request.Title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? "Chưa có mô tả chi tiết cho sự cố này." : request.Description,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Cơ sở vật chất" : request.Category,
            RoomCode = request.Room.Code,
            BuildingName = request.Room.Floor.Building.Name,
            FloorNumber = request.Room.Floor.Number,
            ReporterName = request.ReporterStudent?.FullName ?? "Ban quản lý",
            ReporterCode = request.ReporterStudent?.StudentCode ?? "Nội bộ KTX",
            Priority = request.Priority.ToString(),
            PriorityLabel = priority.Label,
            PriorityClass = priority.Css,
            Status = request.Status.ToString(),
            StatusLabel = status.Label,
            StatusClass = status.Css,
            AssignedTeam = request.AssignedTeam,
            BlockedBeds = request.BlockedBeds,
            MaxBlockedBeds = maxBlocked,
            Capacity = capacity.Capacity,
            Occupied = capacity.Occupied,
            Held = capacity.Held,
            Assignable = capacity.Assignable,
            CreatedAtLocal = ToBusinessLocal(request.CreatedAt),
            UpdatedAtLocal = ToBusinessLocal(request.UpdatedAt),
            IsSlaBreached = IsOpen(request.Status) && IsSlaBreached(request.Priority, request.CreatedAt, now),
            CanUpdate = request.Status != MaintenanceStatus.Closed,
            RowVersion = Convert.ToBase64String(request.RowVersion),
            Timeline = timeline,
            Attachments = request.Attachments.OrderBy(x => x.CreatedAt)
                .Select(x => new MaintenanceAttachmentViewModel(x.Id, x.OriginalName, x.ContentType))
                .ToList()
        };
    }

    private IQueryable<MaintenanceProjection> Projections() => db.MaintenanceRequests.AsNoTracking()
        .Select(x => new MaintenanceProjection
        {
            Id = x.Id,
            Code = x.Code,
            Title = x.Title,
            Category = x.Category,
            RoomCode = x.Room.Code,
            BuildingCode = x.Room.Floor.Building.Code,
            Priority = x.Priority,
            Status = x.Status,
            AssignedTeam = x.AssignedTeam,
            BlockedBeds = x.BlockedBeds,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        });

    private static IQueryable<MaintenanceProjection> ApplyFilters(
        IQueryable<MaintenanceProjection> source,
        MaintenanceQueueQuery query,
        DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x => x.Code.Contains(search) || x.Title.Contains(search) || x.RoomCode.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(query.Building))
            source = source.Where(x => x.BuildingCode == query.Building);
        if (Enum.TryParse<MaintenancePriority>(query.Priority, true, out var priority))
            source = source.Where(x => x.Priority == priority);

        source = query.Status?.ToLowerInvariant() switch
        {
            "open" => source.Where(x => x.Status == MaintenanceStatus.New || x.Status == MaintenanceStatus.Assigned || x.Status == MaintenanceStatus.InProgress || x.Status == MaintenanceStatus.WaitingParts),
            "unassigned" => source.Where(x => (x.Status == MaintenanceStatus.New || x.Status == MaintenanceStatus.Assigned || x.Status == MaintenanceStatus.InProgress || x.Status == MaintenanceStatus.WaitingParts) && x.AssignedTeam == null),
            "progress" => source.Where(x => x.Status == MaintenanceStatus.Assigned || x.Status == MaintenanceStatus.InProgress),
            "waiting" => source.Where(x => x.Status == MaintenanceStatus.WaitingParts),
            "resolved" => source.Where(x => x.Status == MaintenanceStatus.Resolved || x.Status == MaintenanceStatus.Closed),
            "breached" => source.Where(x => (x.Status == MaintenanceStatus.New || x.Status == MaintenanceStatus.Assigned || x.Status == MaintenanceStatus.InProgress || x.Status == MaintenanceStatus.WaitingParts) &&
                (x.Priority == MaintenancePriority.Urgent && x.CreatedAt < now.AddHours(-2) ||
                 x.Priority == MaintenancePriority.High && x.CreatedAt < now.AddHours(-8) ||
                 x.Priority == MaintenancePriority.Medium && x.CreatedAt < now.AddHours(-24) ||
                 x.Priority == MaintenancePriority.Low && x.CreatedAt < now.AddHours(-72))),
            _ => source
        };
        return source;
    }

    private static bool IsOpen(MaintenanceStatus status) => OpenStatuses.Contains(status);

    private static bool IsSlaBreached(MaintenancePriority priority, DateTime createdAt, DateTime now) =>
        createdAt < now - (priority switch
            {
                MaintenancePriority.Urgent => TimeSpan.FromHours(2),
                MaintenancePriority.High => TimeSpan.FromHours(8),
                MaintenancePriority.Medium => TimeSpan.FromHours(24),
                _ => TimeSpan.FromHours(72)
            });

    private static bool CanTransition(MaintenanceStatus current, MaintenanceStatus next)
    {
        if (current == next) return true;
        if (current == MaintenanceStatus.Closed) return false;
        if (current == MaintenanceStatus.Resolved)
            return next is MaintenanceStatus.InProgress or MaintenanceStatus.Closed;
        return true;
    }

    private static (string Label, string Css) PriorityPresentation(MaintenancePriority priority) => priority switch
    {
        MaintenancePriority.Urgent => ("Khẩn cấp", "urgent"),
        MaintenancePriority.High => ("Ưu tiên cao", "high"),
        MaintenancePriority.Medium => ("Ưu tiên vừa", "medium"),
        _ => ("Ưu tiên thấp", "low")
    };

    private static (string Label, string Css) StatusPresentation(MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.New => ("Chưa phân công", "new"),
        MaintenanceStatus.Assigned => ("Đã phân công", "assigned"),
        MaintenanceStatus.InProgress => ("Đang xử lý", "progress"),
        MaintenanceStatus.WaitingParts => ("Chờ vật tư", "waiting"),
        MaintenanceStatus.Resolved => ("Đã hoàn thành", "resolved"),
        _ => ("Đã đóng", "closed")
    };

    private static string AgeLabel(DateTime value, DateTime now)
    {
        var age = value == default ? TimeSpan.Zero : now - value;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age.TotalMinutes < 60) return $"{Math.Max(1, (int)age.TotalMinutes)} phút trước";
        if (age.TotalHours < 24) return $"{(int)age.TotalHours} giờ trước";
        return $"{Math.Max(1, (int)age.TotalDays)} ngày trước";
    }

    private bool TrySetOriginalVersion(MaintenanceRequest request, string encoded)
    {
        try
        {
            var version = Convert.FromBase64String(encoded);
            if (version.Length == 0) return false;
            db.Entry(request).Property(x => x.RowVersion).OriginalValue = version;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static DateTime ToBusinessLocal(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, BusinessTimeZone), DateTimeKind.Unspecified);
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("Asia/Saigon", TimeSpan.FromHours(7), "Asia/Saigon", "Asia/Saigon");
    }

    private sealed class MaintenanceProjection
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string RoomCode { get; init; } = string.Empty;
        public string BuildingCode { get; init; } = string.Empty;
        public MaintenancePriority Priority { get; init; }
        public MaintenanceStatus Status { get; init; }
        public string? AssignedTeam { get; init; }
        public int BlockedBeds { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
