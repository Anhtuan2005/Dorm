using System.Data;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class ApplicationManagementService(DormitoryDbContext db) : IApplicationManagementService
{
    private static readonly HousingRequestStatus[] ActiveStatuses =
        [HousingRequestStatus.Submitted, HousingRequestStatus.UnderReview, HousingRequestStatus.NeedsInformation];
    private static readonly MaintenanceStatus[] OpenMaintenanceStatuses =
        [MaintenanceStatus.New, MaintenanceStatus.Assigned, MaintenanceStatus.InProgress, MaintenanceStatus.WaitingParts];

    public Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default) =>
        db.HousingRequests.CountAsync(x => ActiveStatuses.Contains(x.Status), cancellationToken);

    public async Task<ApplicationQueueViewModel> GetQueueAsync(
        ApplicationQueueQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var overdueAt = now.AddHours(-24);
        var requests = await db.HousingRequests.AsNoTracking()
            .Include(x => x.StudentProfile).ThenInclude(x => x.User)
            .Include(x => x.StudentProfile).ThenInclude(x => x.Stays.Where(stay => stay.EndedAt == null))
                .ThenInclude(x => x.Room).ThenInclude(x => x.Floor).ThenInclude(x => x.Building)
            .Include(x => x.TargetRoom).ThenInclude(x => x!.Floor).ThenInclude(x => x.Building)
            .Include(x => x.Events).ThenInclude(x => x.ActorUser)
            .AsSplitQuery()
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync(cancellationToken);

        var summary = new ApplicationQueueSummaryViewModel(
            requests.Count(x => ActiveStatuses.Contains(x.Status)),
            requests.Count(x => x.Status == HousingRequestStatus.Submitted),
            requests.Count(x => ActiveStatuses.Contains(x.Status) && x.SubmittedAt < overdueAt),
            requests.Count(x => x.Status == HousingRequestStatus.UnderReview),
            requests.Count(x => x.Status == HousingRequestStatus.NeedsInformation),
            requests.Count(x => x.Status is HousingRequestStatus.Approved or HousingRequestStatus.Rejected && x.ReviewedAt >= now.Date));

        IEnumerable<HousingRequest> filtered = query.Scope switch
        {
            "new" => requests.Where(x => x.Status == HousingRequestStatus.Submitted),
            "review" => requests.Where(x => x.Status == HousingRequestStatus.UnderReview),
            "info" => requests.Where(x => x.Status == HousingRequestStatus.NeedsInformation),
            "completed" => requests.Where(x => !ActiveStatuses.Contains(x.Status)),
            "all" => requests,
            _ => requests.Where(x => ActiveStatuses.Contains(x.Status))
        };

        if (query.OverdueOnly)
            filtered = filtered.Where(x => ActiveStatuses.Contains(x.Status) && x.SubmittedAt < overdueAt);
        if (!string.IsNullOrWhiteSpace(query.Type) && Enum.TryParse<HousingRequestType>(query.Type, true, out var type))
            filtered = filtered.Where(x => x.Type == type);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(x =>
                x.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.StudentProfile.StudentCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.StudentProfile.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (x.TargetRoom?.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var ordered = filtered
            .OrderByDescending(x => ActiveStatuses.Contains(x.Status) && x.SubmittedAt < overdueAt)
            .ThenBy(x => x.Status == HousingRequestStatus.UnderReview ? 0 : x.Status == HousingRequestStatus.Submitted ? 1 : 2)
            .ThenBy(x => x.SubmittedAt)
            .ToList();
        var selected = ordered.FirstOrDefault(x => x.Id == query.SelectedId) ?? ordered.FirstOrDefault();

        var items = ordered.Select(request =>
        {
            var status = Status(request.Status);
            var currentStay = request.StudentProfile.Stays.SingleOrDefault();
            return new ApplicationQueueItemViewModel(
                request.Id,
                request.Code,
                request.StudentProfile.StudentCode,
                request.StudentProfile.FullName,
                Initials(request.StudentProfile.FullName),
                TypeLabel(request.Type),
                status.Label,
                status.Css,
                currentStay?.Room.Code,
                request.TargetRoom?.Code,
                request.SubmittedAt,
                Math.Max(0, (int)(now - request.SubmittedAt).TotalHours),
                ActiveStatuses.Contains(request.Status) && request.SubmittedAt < overdueAt,
                request.Id == selected?.Id);
        }).ToList();

        return new ApplicationQueueViewModel
        {
            Query = query,
            Summary = summary,
            Items = items,
            SelectedCase = selected is null ? null : await ToCaseAsync(selected, now, cancellationToken)
        };
    }

    public Task<ApplicationManagementResult> BeginReviewAsync(
        ApplicationActionInput input,
        string managerUserId,
        CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(input, managerUserId, HousingRequestStatus.UnderReview,
            "Quản lý đã bắt đầu kiểm tra hồ sơ.", false, cancellationToken);

    public Task<ApplicationManagementResult> RequestInformationAsync(
        ApplicationActionInput input,
        string managerUserId,
        CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(input, managerUserId, HousingRequestStatus.NeedsInformation,
            input.Reason?.Trim() ?? string.Empty, true, cancellationToken);

    public Task<ApplicationManagementResult> RejectAsync(
        ApplicationActionInput input,
        string managerUserId,
        CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(input, managerUserId, HousingRequestStatus.Rejected,
            input.Reason?.Trim() ?? string.Empty, true, cancellationToken);

    public async Task<ApplicationManagementResult> ApproveAndAssignAsync(
        ApplicationActionInput input,
        string managerUserId,
        CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() =>
            ApproveWithinTransactionAsync(input, managerUserId, cancellationToken));
    }

    private async Task<ApplicationManagementResult> ApproveWithinTransactionAsync(
        ApplicationActionInput input,
        string managerUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var request = await db.HousingRequests
                .Include(x => x.StudentProfile).ThenInclude(x => x.Stays.Where(stay => stay.EndedAt == null))
                .Include(x => x.TargetRoom).ThenInclude(x => x!.Stays.Where(stay => stay.EndedAt == null))
                .Include(x => x.TargetRoom).ThenInclude(x => x!.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > DateTime.UtcNow))
                .Include(x => x.TargetRoom).ThenInclude(x => x!.MaintenanceRequests.Where(repair => OpenMaintenanceStatuses.Contains(repair.Status)))
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken);

            if (request is null) return new(false, "Không tìm thấy đơn đăng ký.");
            if (!TrySetOriginalVersion(request, input.RowVersion)) return new(false, "Phiên bản đơn không hợp lệ. Vui lòng tải lại.", request.Id);
            if (!ActiveStatuses.Contains(request.Status)) return new(false, "Đơn này đã được xử lý trước đó.", request.Id);
            if (request.TargetRoom is null) return new(false, "Đơn chưa có phòng đích để xếp.", request.Id);

            var room = request.TargetRoom;
            if (!string.Equals(room.RoomType, request.StudentProfile.Gender, StringComparison.OrdinalIgnoreCase))
                return new(false, "Phòng đích không phù hợp chính sách lưu trú trong hồ sơ sinh viên.", request.Id);

            var currentStay = request.StudentProfile.Stays.SingleOrDefault();
            if (currentStay?.RoomId == room.Id)
                return new(false, "Sinh viên hiện đã ở phòng đích.", request.Id);

            var blocked = Math.Min(Math.Max(0, room.Capacity - room.Stays.Count), room.MaintenanceRequests.Sum(x => x.BlockedBeds));
            var held = Math.Min(Math.Max(0, room.Capacity - room.Stays.Count - blocked), room.Holds.Sum(x => x.HeldBeds));
            var assignable = room.Capacity - room.Stays.Count - blocked - held;
            if (assignable <= 0)
                return new(false, $"Phòng {room.Code} vừa hết chỗ hoặc đang bị khóa. Không có thay đổi nào được ghi nhận.", request.Id);

            var now = DateTime.UtcNow;
            if (currentStay is not null) currentStay.EndedAt = now;
            db.Stays.Add(new Stay
            {
                StudentProfileId = request.StudentProfileId,
                RoomId = room.Id,
                StartedAt = now
            });

            request.Status = HousingRequestStatus.Approved;
            request.DecisionReason = string.IsNullOrWhiteSpace(input.Reason)
                ? $"Đã xác nhận xếp sinh viên vào phòng {room.Code}."
                : input.Reason.Trim();
            request.ReviewedAt = now;
            request.ReviewedByUserId = managerUserId;
            request.Events.Add(NewEvent(request.Id, HousingRequestStatus.Approved, request.DecisionReason, managerUserId, now));
            AddNotification(request.StudentProfile.UserId, "Application", $"Đơn {request.Code} đã được duyệt",
                $"Bạn đã được xếp vào phòng {room.Code}. Vui lòng xem chi tiết đơn.", "/Student/Applications", now);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, $"Đã duyệt {request.Code} và xếp {request.StudentProfile.FullName} vào phòng {room.Code}.", request.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Đơn đã được người khác cập nhật. Vui lòng tải lại trước khi quyết định.", input.Id);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Dữ liệu phòng vừa thay đổi. Hệ thống chưa xếp phòng; vui lòng kiểm tra lại.", input.Id);
        }
    }

    private async Task<ApplicationManagementResult> UpdateStatusAsync(
        ApplicationActionInput input,
        string managerUserId,
        HousingRequestStatus nextStatus,
        string note,
        bool reasonRequired,
        CancellationToken cancellationToken)
    {
        if (reasonRequired && note.Length < 8)
            return new(false, "Vui lòng nhập nội dung ít nhất 8 ký tự để sinh viên hiểu cần làm gì.", input.Id);

        var request = await db.HousingRequests
            .Include(x => x.StudentProfile)
            .SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken);
        if (request is null) return new(false, "Không tìm thấy đơn đăng ký.");
        if (!TrySetOriginalVersion(request, input.RowVersion)) return new(false, "Phiên bản đơn không hợp lệ. Vui lòng tải lại.", request.Id);
        if (!ActiveStatuses.Contains(request.Status)) return new(false, "Đơn này đã được xử lý trước đó.", request.Id);

        var now = DateTime.UtcNow;
        request.Status = nextStatus;
        request.ReviewedByUserId = managerUserId;
        request.ReviewedAt = now;
        request.DecisionReason = note;
        request.Events.Add(NewEvent(request.Id, nextStatus, note, managerUserId, now));

        var notification = nextStatus switch
        {
            HousingRequestStatus.NeedsInformation => ("Đơn cần bổ sung thông tin", $"Đơn {request.Code}: {note}"),
            HousingRequestStatus.Rejected => ("Đơn đăng ký chưa được chấp thuận", $"Đơn {request.Code}: {note}"),
            _ => ("Đơn đang được xét duyệt", $"Ban quản lý đã bắt đầu xử lý đơn {request.Code}.")
        };
        AddNotification(request.StudentProfile.UserId, "Application", notification.Item1, notification.Item2,
            "/Student/Applications", now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var message = nextStatus switch
            {
                HousingRequestStatus.NeedsInformation => $"Đã yêu cầu bổ sung cho đơn {request.Code}.",
                HousingRequestStatus.Rejected => $"Đã từ chối đơn {request.Code}.",
                _ => $"Đã nhận xử lý đơn {request.Code}."
            };
            return new(true, message, request.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Đơn đã được người khác cập nhật. Vui lòng tải lại.", request.Id);
        }
    }

    private async Task<ApplicationCaseViewModel> ToCaseAsync(
        HousingRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var currentStay = request.StudentProfile.Stays.SingleOrDefault();
        var currentRoom = currentStay is null
            ? null
            : await LoadRoomAsync(currentStay.RoomId, cancellationToken);
        var targetRoom = request.TargetRoomId is null
            ? null
            : await LoadRoomAsync(request.TargetRoomId.Value, cancellationToken);
        var target = targetRoom is null ? null : ToPlacementRoom(targetRoom, now);
        var current = currentRoom is null ? null : ToPlacementRoom(currentRoom, now);
        var status = Status(request.Status);
        var isActive = ActiveStatuses.Contains(request.Status);
        var approvalBlock = target switch
        {
            null => "Đơn chưa có phòng đích.",
            { Assignable: <= 0 } => $"Phòng {target.Code} hiện không còn chỗ có thể xếp.",
            _ when !string.Equals(target.RoomType, request.StudentProfile.Gender, StringComparison.OrdinalIgnoreCase) => "Loại phòng không phù hợp hồ sơ sinh viên.",
            _ when current?.Code == target.Code => "Sinh viên hiện đã ở phòng đích.",
            _ => null
        };
        var outstanding = await db.Invoices.AsNoTracking()
            .Where(x => x.StudentProfileId == request.StudentProfileId && x.Status != InvoiceStatus.Cancelled)
            .SumAsync(x => x.TotalAmount - x.PaidAmount, cancellationToken);
        var repairCount = await db.MaintenanceRequests.AsNoTracking()
            .CountAsync(x => x.ReporterStudentId == request.StudentProfileId && OpenMaintenanceStatuses.Contains(x.Status), cancellationToken);
        var events = request.Events.OrderByDescending(x => x.CreatedAt)
            .Select(x =>
            {
                var eventStatus = Status(x.Status);
                return new ApplicationAuditEventViewModel(
                    eventStatus.Label,
                    eventStatus.Css,
                    x.Note,
                    x.ActorUser?.FullName ?? (x.ActorUserId is null ? "Hệ thống" : request.StudentProfile.FullName),
                    x.CreatedAt);
            }).ToList();
        if (events.Count == 0)
        {
            events.Add(new ApplicationAuditEventViewModel(
                "Đã gửi", "submitted", "Sinh viên đã gửi yêu cầu.", request.StudentProfile.FullName, request.SubmittedAt));
        }

        return new ApplicationCaseViewModel
        {
            Id = request.Id,
            StudentId = request.StudentProfileId,
            Code = request.Code,
            RowVersion = Convert.ToBase64String(request.RowVersion),
            StudentCode = request.StudentProfile.StudentCode,
            StudentName = request.StudentProfile.FullName,
            Initials = Initials(request.StudentProfile.FullName),
            Gender = request.StudentProfile.Gender,
            Faculty = request.StudentProfile.Faculty,
            PhoneNumber = request.StudentProfile.PhoneNumber,
            Email = request.StudentProfile.User?.Email,
            TypeLabel = TypeLabel(request.Type),
            StatusLabel = status.Label,
            StatusClass = status.Css,
            StudentReason = request.StudentReason,
            DecisionReason = request.DecisionReason,
            SubmittedAt = request.SubmittedAt,
            WaitingHours = Math.Max(0, (int)(now - request.SubmittedAt).TotalHours),
            IsOverdue = isActive && request.SubmittedAt < now.AddHours(-24),
            CurrentRoom = current,
            TargetRoom = target,
            OutstandingBalance = Math.Max(0, outstanding),
            OpenRepairCount = repairCount,
            CanStartReview = request.Status is HousingRequestStatus.Submitted or HousingRequestStatus.NeedsInformation,
            CanDecide = isActive,
            CanApprove = isActive && approvalBlock is null,
            ApprovalBlockReason = approvalBlock,
            Events = events
        };
    }

    private async Task<Room?> LoadRoomAsync(int roomId, CancellationToken cancellationToken) =>
        await db.Rooms.AsNoTracking()
            .Include(x => x.Floor).ThenInclude(x => x.Building)
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
            .Include(x => x.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > DateTime.UtcNow))
            .Include(x => x.MaintenanceRequests.Where(repair => OpenMaintenanceStatuses.Contains(repair.Status)))
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == roomId, cancellationToken);

    private static PlacementRoomViewModel ToPlacementRoom(Room room, DateTime now)
    {
        var occupied = room.Stays.Count(x => x.EndedAt == null);
        var blocked = Math.Min(Math.Max(0, room.Capacity - occupied),
            room.MaintenanceRequests.Where(x => OpenMaintenanceStatuses.Contains(x.Status)).Sum(x => x.BlockedBeds));
        var held = Math.Min(Math.Max(0, room.Capacity - occupied - blocked),
            room.Holds.Where(x => x.ReleasedAt == null && x.ExpiresAt > now).Sum(x => x.HeldBeds));
        var assignable = Math.Max(0, room.Capacity - occupied - blocked - held);
        var (label, css) = blocked > 0
            ? ("Đang bảo trì", "maintenance")
            : assignable > 0
                ? ($"Còn {assignable} chỗ", "available")
                : held > 0
                    ? ($"Đang giữ {held} chỗ", "held")
                    : ("Đã đầy", "full");
        return new PlacementRoomViewModel(
            room.Code, room.Floor.Building.Name, room.Floor.Number, room.RoomType,
            room.Capacity, occupied, held, blocked, assignable, room.MonthlyPrice, label, css);
    }

    private bool TrySetOriginalVersion(HousingRequest request, string rowVersion)
    {
        try
        {
            db.Entry(request).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static HousingRequestEvent NewEvent(
        long requestId,
        HousingRequestStatus status,
        string note,
        string actorUserId,
        DateTime createdAt) => new()
    {
        HousingRequestId = requestId,
        Status = status,
        Note = note,
        ActorUserId = actorUserId,
        CreatedAt = createdAt
    };

    private void AddNotification(
        string? userId,
        string type,
        string title,
        string body,
        string targetUrl,
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        var safeBody = body.Length <= 300 ? body : string.Concat(body.AsSpan(0, 297), "...");
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = safeBody,
            TargetUrl = targetUrl,
            CreatedAt = createdAt
        });
    }

    private static string Initials(string name) => string.Concat(name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .TakeLast(2)
        .Select(x => x[0]));

    private static string TypeLabel(HousingRequestType type) => type switch
    {
        HousingRequestType.Registration => "Đăng ký ở",
        HousingRequestType.Transfer => "Chuyển phòng",
        _ => "Trả phòng"
    };

    private static (string Label, string Css) Status(HousingRequestStatus status) => status switch
    {
        HousingRequestStatus.Submitted => ("Đơn mới", "submitted"),
        HousingRequestStatus.UnderReview => ("Đang xét duyệt", "reviewing"),
        HousingRequestStatus.NeedsInformation => ("Cần bổ sung", "needs-info"),
        HousingRequestStatus.Approved => ("Đã duyệt", "approved"),
        HousingRequestStatus.Rejected => ("Từ chối", "rejected"),
        _ => ("Đã hủy", "cancelled")
    };
}
