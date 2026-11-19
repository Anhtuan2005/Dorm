using System.Data;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class ApplicationManagementService(
    DormitoryDbContext db,
    IBillingLedgerService billingLedger,
    TimeProvider timeProvider,
    RealtimeUpdates? live = null) : IApplicationManagementService
{
    private const int PageSize = 24;
    private const string SupplementLabel = "Đã bổ sung";
    private const string LegacySupplementPrefix = "Sinh viên bổ sung: ";
    private static readonly MaintenanceStatus[] OpenMaintenanceStatuses =
        [MaintenanceStatus.New, MaintenanceStatus.Assigned, MaintenanceStatus.InProgress, MaintenanceStatus.WaitingParts];

    public Task<int> GetManagementWorkCountAsync(CancellationToken cancellationToken = default) =>
        db.HousingRequests.CountAsync(x => HousingRequestWorkflow.ManagementStatuses.Contains(x.Status), cancellationToken);

    public async Task<ApplicationQueueViewModel> GetQueueAsync(
        ApplicationQueueQuery query,
        CancellationToken cancellationToken = default)
    {
        query.Page = Math.Max(1, query.Page);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var overdueAt = now.AddHours(-HousingRequestWorkflow.ManagerSlaHours);

        var counts = await db.HousingRequests
            .GroupBy(x => x.Status)
            .Select(group => new {
                Status = group.Key, Total = group.Count(),
                Overdue = group.Count(x => (x.ActionRequiredAt ?? x.SubmittedAt) <= overdueAt),
                ReviewedToday = group.Count(x => x.ReviewedAt >= now.Date)
            })
            .ToListAsync(cancellationToken);
        var summary = new ApplicationQueueSummaryViewModel(
            counts.Where(x => HousingRequestWorkflow.IsActive(x.Status)).Sum(x => x.Total),
            counts.SingleOrDefault(x => x.Status == HousingRequestStatus.Submitted)?.Total ?? 0,
            counts.Where(x => IsManagerWork(x.Status)).Sum(x => x.Overdue),
            counts.SingleOrDefault(x => x.Status == HousingRequestStatus.UnderReview)?.Total ?? 0,
            counts.SingleOrDefault(x => x.Status == HousingRequestStatus.NeedsInformation)?.Total ?? 0,
            counts.Where(x => x.Status is HousingRequestStatus.Approved or HousingRequestStatus.Rejected
                or HousingRequestStatus.Cancelled or HousingRequestStatus.Expired).Sum(x => x.ReviewedToday));

        var filtered = ApplyFilters(Projections(), query, overdueAt);
        var totalItems = await filtered.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / PageSize));
        query.Page = Math.Min(query.Page, totalPages);

        var rows = await filtered
            .OrderByDescending(x => x.SubmittedAt)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var selectedId = query.SelectedId is long requestedId &&
            (rows.Any(x => x.Id == requestedId) || await filtered.AnyAsync(x => x.Id == requestedId, cancellationToken))
            ? requestedId
            : rows.FirstOrDefault(x => x.Code == query.Search?.Trim())?.Id ?? rows.FirstOrDefault()?.Id;
        query.SelectedId = selectedId;

        var items = rows.Select(x =>
        {
            var workflow = HousingRequestWorkflow.Describe(x.Status, x.ActionRequiredAt ?? x.SubmittedAt, now);
            return new ApplicationQueueItemViewModel(
                x.Id,
                x.Code,
                x.StudentCode,
                x.StudentName,
                Initials(x.StudentName),
                HousingRequestWorkflow.TypeLabel(x.Type),
                x.ReturnedWithSupplement ? SupplementLabel : workflow.StatusLabel,
                workflow.StatusClass,
                x.CurrentRoomCode,
                x.TargetRoomCode,
                x.SubmittedAt,
                workflow.WaitingHours,
                workflow.IsManagerOverdue,
                workflow.ResponsibilityLabel,
                workflow.NextActionLabel,
                x.Id == selectedId);
        }).ToList();

        return new ApplicationQueueViewModel
        {
            Query = query,
            Summary = summary,
            Items = items,
            SelectedCase = selectedId.HasValue
                ? await LoadCaseAsync(selectedId.Value, now, cancellationToken)
                : null,
            TotalItems = totalItems,
            PageSize = PageSize
        };
    }

    public Task<ApplicationManagementResult> ExecuteAsync(
        ApplicationActionInput input,
        HousingRequestAction action,
        string managerUserId,
        CancellationToken cancellationToken = default)
    {
        if (action == HousingRequestAction.Approve)
        {
            var strategy = db.Database.CreateExecutionStrategy();
            return strategy.ExecuteAsync(() => ApproveWithinTransactionAsync(input, managerUserId, cancellationToken));
        }

        if (action == HousingRequestAction.ConfirmMoveIn)
        {
            var strategy = db.Database.CreateExecutionStrategy();
            return strategy.ExecuteAsync(() => ConfirmMoveInWithinTransactionAsync(input, managerUserId, cancellationToken));
        }

        if (action is not (HousingRequestAction.BeginReview or HousingRequestAction.RequestInformation or HousingRequestAction.Reject))
            return Task.FromResult(new ApplicationManagementResult(false, "Thao tác quản lý không hợp lệ.", input.Id));

        var note = action switch
        {
            HousingRequestAction.BeginReview => "Quản lý đã nhận và bắt đầu kiểm tra hồ sơ.",
            _ => input.Reason?.Trim() ?? string.Empty
        };
        if (action is HousingRequestAction.RequestInformation or HousingRequestAction.Reject && note.Length < 8)
            return Task.FromResult(new ApplicationManagementResult(false, "Vui lòng nhập nội dung ít nhất 8 ký tự để sinh viên hiểu cần làm gì.", input.Id));

        var updateStrategy = db.Database.CreateExecutionStrategy();
        return updateStrategy.ExecuteAsync(() => TransitionWithinTransactionAsync(
            input, action, note, managerUserId, cancellationToken));
    }

    private async Task<ApplicationManagementResult> TransitionWithinTransactionAsync(
        ApplicationActionInput input,
        HousingRequestAction action,
        string note,
        string managerUserId,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var request = await db.HousingRequests
                .Include(x => x.StudentProfile)
                .Include(x => x.ApprovedRoomHold)
                .SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken);
            if (request is null) return new(false, "Không tìm thấy đơn lưu trú.");
            if (!TrySetOriginalVersion(request, input.RowVersion))
                return new(false, "Phiên bản đơn không hợp lệ. Vui lòng tải lại.", request.Id);

            var transition = HousingRequestWorkflow.Transition(request.Status, action);
            if (!transition.Allowed) return new(false, transition.ErrorMessage, request.Id);

            var now = timeProvider.GetUtcNow().UtcDateTime;
            request.Status = transition.NextStatus;
            request.ReviewedByUserId = managerUserId;
            request.ReviewedAt = now;
            request.ActionRequiredAt = action == HousingRequestAction.RequestInformation
                ? now
                : action == HousingRequestAction.Reject
                    ? null
                    : request.ActionRequiredAt ?? request.SubmittedAt;
            if (action is HousingRequestAction.RequestInformation or HousingRequestAction.Reject)
                request.DecisionReason = note;
            if (action == HousingRequestAction.Reject && request.ApprovedRoomHold is not null && request.ApprovedRoomHold.ReleasedAt is null)
                request.ApprovedRoomHold.ReleasedAt = now;
            request.Events.Add(NewEvent(request.Id, transition.NextStatus, note, managerUserId, now));

            var notification = action switch
            {
                HousingRequestAction.RequestInformation => ("Đơn cần bổ sung thông tin", $"Đơn {request.Code}: {note}"),
                HousingRequestAction.Reject => ("Đơn lưu trú chưa được chấp thuận", $"Đơn {request.Code}: {note}"),
                _ => ("Đơn đang được xét duyệt", $"Ban quản lý đã bắt đầu xử lý đơn {request.Code}.")
            };
            AddNotification(request.StudentProfile.UserId, notification.Item1, notification.Item2, now);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (live is not null) await live.NotifyOperationsAsync(request.StudentProfile.UserId);
            return new(true, action switch
            {
                HousingRequestAction.RequestInformation => $"Đã yêu cầu bổ sung cho đơn {request.Code}.",
                HousingRequestAction.Reject => $"Đã từ chối đơn {request.Code}.",
                _ => $"Đã nhận xử lý đơn {request.Code}."
            }, request.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Đơn đã được người khác cập nhật. Vui lòng tải lại.", input.Id);
        }
    }

    private async Task<ApplicationManagementResult> ApproveWithinTransactionAsync(
        ApplicationActionInput input,
        string managerUserId,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var request = await db.HousingRequests
                .Include(x => x.ApprovedRoomHold)
                .Include(x => x.StudentProfile).ThenInclude(x => x.Stays.Where(stay => stay.EndedAt == null))
                .Include(x => x.TargetRoom).ThenInclude(x => x!.Stays.Where(stay => stay.EndedAt == null))
                .Include(x => x.TargetRoom).ThenInclude(x => x!.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now))
                .Include(x => x.TargetRoom).ThenInclude(x => x!.MaintenanceRequests.Where(repair => OpenMaintenanceStatuses.Contains(repair.Status)))
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken);

            if (request is null) return new(false, "Không tìm thấy đơn lưu trú.");
            if (!TrySetOriginalVersion(request, input.RowVersion))
                return new(false, "Phiên bản đơn không hợp lệ. Vui lòng tải lại.", request.Id);

            var transition = HousingRequestWorkflow.Transition(request.Status, HousingRequestAction.Approve, request.Type);
            if (!transition.Allowed) return new(false, transition.ErrorMessage, request.Id);

            var currentStay = request.StudentProfile.Stays.SingleOrDefault();
            var validationError = ValidateApproval(request, currentStay, request.TargetRoom, now);
            if (validationError is not null) return new(false, validationError, request.Id);

            if (request.Type == HousingRequestType.Checkout)
            {
                var inventoryError = RecordInventory(request, input);
                if (inventoryError != null) return new(false, inventoryError, request.Id);
                currentStay!.EndedAt = now;
            }

            if (request.Type is HousingRequestType.Registration or HousingRequestType.Transfer)
            {
                request.TargetBedNumber ??= RoomBedPolicy.Available(request.TargetRoom!, now)[0];
                var hold = request.ApprovedRoomHold ??= new RoomHold();
                hold.RoomId = request.TargetRoomId!.Value;
                hold.HeldBeds = 1;
                hold.BedNumber = request.TargetBedNumber;
                hold.ReleasedAt = null;
                hold.ExpiresAt = now.AddDays(7);
            }

            request.Status = transition.NextStatus;
            request.ActionRequiredAt = request.Type == HousingRequestType.Checkout ? null : now;
            request.DecisionReason = string.IsNullOrWhiteSpace(input.Reason)
                ? request.Type == HousingRequestType.Checkout
                    ? "Đã xác nhận trả phòng và kết thúc lượt lưu trú."
                    : $"Đã duyệt phòng {request.TargetRoom!.Code}; giữ giường {request.TargetBedNumber:00} trong 7 ngày để hoàn tất nhận phòng."
                : input.Reason.Trim();
            request.ReviewedAt = now;
            request.ReviewedByUserId = managerUserId;
            request.Events.Add(NewEvent(request.Id, request.Status, request.DecisionReason, managerUserId, now));

            var outcome = request.Type == HousingRequestType.Checkout
                ? "Yêu cầu trả phòng đã được duyệt. Lượt lưu trú hiện tại đã kết thúc."
                : $"Đã giữ giường {request.TargetBedNumber:00} tại {request.TargetRoom!.Code} đến {now.AddDays(7).AddHours(7):dd/MM/yyyy}. Hãy liên hệ Ban quản lý để nhận phòng.";
            AddNotification(request.StudentProfile.UserId, request.Type == HousingRequestType.Checkout ? $"Đơn {request.Code} đã được duyệt" : $"Đơn {request.Code} chờ nhận phòng", outcome, now);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (live is not null) await live.NotifyOperationsAsync(request.StudentProfile.UserId);
            return request.Type == HousingRequestType.Checkout
                ? new(true, $"Đã duyệt {request.Code} và xác nhận trả phòng.", request.Id)
                : new(true, $"Đã duyệt {request.Code} và giữ giường {request.TargetBedNumber:00} tại {request.TargetRoom!.Code} trong 7 ngày. Chưa bắt đầu lượt ở.", request.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Đơn hoặc lượt lưu trú đã được người khác cập nhật. Vui lòng tải lại trước khi quyết định.", input.Id);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Dữ liệu lưu trú vừa thay đổi. Hệ thống chưa ghi quyết định; vui lòng kiểm tra lại.", input.Id);
        }
    }

    private async Task<ApplicationManagementResult> ConfirmMoveInWithinTransactionAsync(
        ApplicationActionInput input,
        string managerUserId,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var request = await db.HousingRequests
                .Include(x => x.StudentProfile).ThenInclude(x => x.Stays.Where(stay => stay.EndedAt == null))
                .Include(x => x.ApprovedRoomHold)
                .Include(x => x.TargetRoom).ThenInclude(x => x!.Stays.Where(stay => stay.EndedAt == null))
                .Include(x => x.TargetRoom).ThenInclude(x => x!.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now))
                .Include(x => x.TargetRoom).ThenInclude(x => x!.MaintenanceRequests.Where(repair => OpenMaintenanceStatuses.Contains(repair.Status)))
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken);
            if (request is null) return new(false, "Không tìm thấy đơn lưu trú.");
            if (!TrySetOriginalVersion(request, input.RowVersion)) return new(false, "Phiên bản đơn không hợp lệ. Vui lòng tải lại.", request.Id);
            var transition = HousingRequestWorkflow.Transition(request.Status, HousingRequestAction.ConfirmMoveIn);
            if (!transition.Allowed) return new(false, transition.ErrorMessage, request.Id);
            if (request.TargetRoom is null || request.ApprovedRoomHold is null) return new(false, "Đơn không còn thông tin giữ chỗ hợp lệ.", request.Id);

            var currentStay = request.StudentProfile.Stays.SingleOrDefault();
            var moveInError = ValidateMoveIn(request, currentStay, request.TargetRoom, now);
            if (moveInError is not null) return new(false, moveInError, request.Id);

            var inventoryError = RecordInventory(request, input);
            if (inventoryError != null) return new(false, inventoryError, request.Id);
            if (input.PlannedEnd is { } end && end < DateOnly.FromDateTime(now.AddHours(7)))
                return new(false, "Ngày hết hạn lưu trú phải từ hôm nay trở đi.", request.Id);
            var hold = request.ApprovedRoomHold;
            request.TargetBedNumber = hold.BedNumber ??
                RoomBedPolicy.Available(request.TargetRoom, now, excludingHoldId: hold.Id)[0];
            hold.BedNumber = request.TargetBedNumber;
            if (request.Type == HousingRequestType.Transfer) currentStay!.EndedAt = now;
            hold.ReleasedAt = now;
            db.Stays.Add(new Stay
            {
                StudentProfileId = request.StudentProfileId,
                RoomId = request.TargetRoom.Id,
                BedNumber = request.TargetBedNumber,
                HousingRequestId = request.Id,
                PlannedEnd = input.PlannedEnd ?? currentStay?.PlannedEnd,
                StartedAt = now
            });
            request.Status = transition.NextStatus;
            request.ActionRequiredAt = null;
            request.MoveInConfirmedAt = now;
            request.DecisionReason = string.IsNullOrWhiteSpace(input.Reason)
                ? $"Đã bàn giao giường {request.TargetBedNumber:00}, phòng {request.TargetRoom.Code}."
                : input.Reason.Trim();
            request.Events.Add(NewEvent(request.Id, request.Status, request.DecisionReason, managerUserId, now));
            AddNotification(request.StudentProfile.UserId, $"Đã nhận phòng {request.TargetRoom.Code}", $"Lượt lưu trú của bạn bắt đầu từ {now.AddHours(7):dd/MM/yyyy HH:mm}.", now);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (live is not null) await live.NotifyOperationsAsync(request.StudentProfile.UserId);
            return new(true, $"Đã xác nhận {request.StudentProfile.FullName} nhận phòng {request.TargetRoom.Code}.", request.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Đơn hoặc sức chứa vừa thay đổi. Hãy tải lại trước khi xác nhận nhận phòng.", input.Id);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Không thể tạo lượt ở vì dữ liệu vừa thay đổi. Chưa có thay đổi nào được ghi.", input.Id);
        }
    }

    private static string? ValidateApproval(HousingRequest request, Stay? currentStay, Room? targetRoom, DateTime now)
    {
        if (request.Type == HousingRequestType.Checkout)
        {
            if (request.TargetRoomId is not null) return "Đơn trả phòng không được có phòng đích.";
            if (currentStay is null) return "Sinh viên không còn lượt lưu trú để trả phòng.";
            if (request.SourceStayId != currentStay.Id) return "Lượt lưu trú đã thay đổi sau khi gửi đơn. Hãy kiểm tra lại hồ sơ.";
            return null;
        }

        if (targetRoom is null) return "Đơn chưa có phòng đích để xếp.";
        if (!string.Equals(targetRoom.RoomType, request.StudentProfile.Gender, StringComparison.OrdinalIgnoreCase))
            return "Phòng đích không phù hợp chính sách lưu trú trong hồ sơ sinh viên.";
        if (request.Type == HousingRequestType.Registration && currentStay is not null)
            return "Sinh viên đã có lượt lưu trú; đơn đăng ký ở không còn phù hợp.";
        if (request.Type == HousingRequestType.Transfer && currentStay is null)
            return "Sinh viên không còn lượt lưu trú nguồn để chuyển phòng.";
        if (request.Type == HousingRequestType.Transfer && request.SourceStayId != currentStay!.Id)
            return "Lượt lưu trú nguồn đã thay đổi sau khi gửi đơn. Hãy kiểm tra lại hồ sơ.";
        if (currentStay?.RoomId == targetRoom.Id)
            return "Sinh viên hiện đã ở phòng đích.";

        if (request.ApprovedRoomHold is { ReleasedAt: null } hold && hold.ExpiresAt > now)
            return ValidateMoveIn(request, currentStay, targetRoom, now);

        var decision = RoomCapacityPolicy.CanReserve(targetRoom, requestedBeds: 1, now);
        return decision.Allowed
            ? ValidateRequestedBed(request, targetRoom, now)
            : $"{decision.ErrorMessage} Không có thay đổi nào được ghi nhận.";
    }

    private async Task<ApplicationCaseViewModel?> LoadCaseAsync(long id, DateTime now, CancellationToken cancellationToken)
    {
        var request = await db.HousingRequests.AsNoTracking()
            .Include(x => x.StudentProfile).ThenInclude(x => x.User)
            .Include(x => x.StudentProfile).ThenInclude(x => x.Stays.Where(stay => stay.EndedAt == null))
                .ThenInclude(x => x.Room).ThenInclude(x => x.Floor).ThenInclude(x => x.Building)
            .Include(x => x.TargetRoom).ThenInclude(x => x!.Floor).ThenInclude(x => x.Building)
            .Include(x => x.ApprovedRoomHold)
            .Include(x => x.Events).ThenInclude(x => x.ActorUser)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (request is null) return null;

        var currentStay = request.StudentProfile.Stays.SingleOrDefault();
        var currentRoom = currentStay is null ? null : await LoadRoomAsync(currentStay.RoomId, now, cancellationToken);
        var targetRoom = request.TargetRoomId is null ? null : await LoadRoomAsync(request.TargetRoomId.Value, now, cancellationToken);
        var current = currentRoom is null ? null : ToPlacementRoom(currentRoom, now);
        var target = targetRoom is null ? null : ToPlacementRoom(targetRoom, now);
        var workflow = HousingRequestWorkflow.Describe(request.Status, request.ActionRequiredAt ?? request.SubmittedAt, now);
        var approvalTransition = HousingRequestWorkflow.Transition(request.Status, HousingRequestAction.Approve, request.Type);
        var approvalBlock = approvalTransition.Allowed
            ? ValidateApproval(request, currentStay, targetRoom, now)
            : request.Status switch
            {
                HousingRequestStatus.Submitted => "Nhận xử lý đơn trước khi ra quyết định.",
                HousingRequestStatus.NeedsInformation => "Đang chờ sinh viên bổ sung hồ sơ; chưa thể duyệt ở bước này.",
                HousingRequestStatus.AwaitingMoveIn => ValidateMoveIn(request, currentStay, targetRoom, now),
                _ => null
            };
        var outstanding = await billingLedger.GetOutstandingBalanceAsync(request.StudentProfileId, cancellationToken);
        var repairCount = await db.MaintenanceRequests.AsNoTracking()
            .CountAsync(x => x.ReporterStudentId == request.StudentProfileId && OpenMaintenanceStatuses.Contains(x.Status), cancellationToken);
        // An expiry event can repeat Submitted without being a reply from the student.
        var firstInformationEventId = request.Events
            .Where(x => x.Status == HousingRequestStatus.NeedsInformation)
            .Min(x => (long?)x.Id);
        var events = request.Events.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x =>
            {
                var eventStatus = HousingRequestWorkflow.Presentation(x.Status);
                var isSupplement = x.Status == HousingRequestStatus.Submitted && x.Id > firstInformationEventId &&
                    (x.ActorUserId != null || x.Note.StartsWith(LegacySupplementPrefix, StringComparison.Ordinal));
                var note = isSupplement && x.Note.StartsWith(LegacySupplementPrefix, StringComparison.Ordinal)
                    ? x.Note[LegacySupplementPrefix.Length..]
                    : x.Note;
                return new ApplicationAuditEventViewModel(
                    isSupplement ? SupplementLabel : eventStatus.Label,
                    eventStatus.Css,
                    note,
                    x.ActorUser?.FullName ?? (x.ActorUserId is null ? "Hệ thống" : request.StudentProfile.FullName),
                    x.CreatedAt,
                    isSupplement);
            }).ToList();
        var latestSupplement = events.FirstOrDefault(x => x.IsSupplement);
        if (events.Count == 0)
            events.Add(new("Đã gửi", "submitted", "Sinh viên đã gửi yêu cầu.", request.StudentProfile.FullName, request.SubmittedAt));

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
            TypeLabel = HousingRequestWorkflow.TypeLabel(request.Type),
            RequestType = request.Type.ToString(),
            StatusLabel = request.Status == HousingRequestStatus.Submitted && latestSupplement is not null
                ? SupplementLabel : workflow.StatusLabel,
            StatusClass = workflow.StatusClass,
            StudentReason = request.StudentReason,
            DecisionReason = request.DecisionReason,
            SubmittedAt = request.SubmittedAt,
            WaitingHours = workflow.WaitingHours,
            IsOverdue = workflow.IsManagerOverdue,
            ResponsibilityLabel = workflow.ResponsibilityLabel,
            NextActionLabel = workflow.NextActionLabel,
            CurrentRoom = current,
            TargetRoom = target,
            TargetBedNumber = request.TargetBedNumber,
            OutstandingBalance = Math.Max(0, outstanding),
            OpenRepairCount = repairCount,
            CanStartReview = HousingRequestWorkflow.Can(request.Status, HousingRequestAction.BeginReview),
            CanDecide = workflow.IsActive,
            CanRequestInformation = HousingRequestWorkflow.Can(request.Status, HousingRequestAction.RequestInformation),
            CanReject = HousingRequestWorkflow.Can(request.Status, HousingRequestAction.Reject),
            CanApprove = approvalTransition.Allowed && approvalBlock is null,
            CanConfirmMoveIn = HousingRequestWorkflow.Can(request.Status, HousingRequestAction.ConfirmMoveIn) && approvalBlock is null,
            Inventory = request.Inventory,
            AppointmentUtc = request.AppointmentUtc,
            HoldExpiresAt = request.ApprovedRoomHold?.ExpiresAt,
            HasProvisionalHold = request.Status is HousingRequestStatus.Submitted or HousingRequestStatus.UnderReview or HousingRequestStatus.NeedsInformation
                && request.ApprovedRoomHold is { ReleasedAt: null } held && held.ExpiresAt > now,
            ApprovalBlockReason = approvalBlock,
            LatestSupplement = latestSupplement,
            Events = events
        };
    }

    private static string? ValidateMoveIn(HousingRequest request, Stay? currentStay, Room? targetRoom, DateTime now)
    {
        if (request.Type is not (HousingRequestType.Registration or HousingRequestType.Transfer))
            return "Loại đơn không hỗ trợ xác nhận nhận phòng.";
        if (targetRoom is null || request.TargetRoomId != targetRoom.Id)
            return "Đơn không còn phòng đích hợp lệ.";
        if (!string.Equals(targetRoom.RoomType, request.StudentProfile.Gender, StringComparison.OrdinalIgnoreCase))
            return "Phòng đích không còn phù hợp chính sách lưu trú trong hồ sơ sinh viên.";
        if (request.Type == HousingRequestType.Registration && currentStay is not null)
            return "Sinh viên đã có lượt lưu trú khác.";
        if (request.Type == HousingRequestType.Transfer && (currentStay is null || request.SourceStayId != currentStay.Id))
            return "Lượt lưu trú nguồn đã thay đổi; chưa thể nhận phòng.";

        var holdDecision = RoomCapacityPolicy.CanConsumeHold(targetRoom, request.ApprovedRoomHold, requiredBeds: 1, now);
        if (!holdDecision.Allowed) return holdDecision.ErrorMessage;

        var hold = request.ApprovedRoomHold!;
        if (request.TargetBedNumber is int requested && requested != hold.BedNumber)
            return "Chỗ giữ không đúng số giường của đơn.";
        var available = RoomBedPolicy.Available(targetRoom, now, excludingHoldId: hold.Id);
        return (hold.BedNumber is int number ? available.Contains(number) : available.Count > 0)
            ? null : "Giường giữ cho đơn không còn có thể bàn giao. Hãy kiểm tra lại chỗ giữ.";
    }

    private static string? ValidateRequestedBed(HousingRequest request, Room room, DateTime now)
    {
        var available = RoomBedPolicy.Available(room, now);
        if (request.TargetBedNumber is int number && !available.Contains(number))
            return $"Giường {number:00} không còn có thể xếp. Hãy từ chối đơn để sinh viên chọn lại.";
        return available.Count == 0 ? "Phòng không còn giường có thể xếp." : null;
    }

    private async Task<Room?> LoadRoomAsync(int roomId, DateTime now, CancellationToken cancellationToken) =>
        await db.Rooms.AsNoTracking()
            .Include(x => x.Floor).ThenInclude(x => x.Building)
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
            .Include(x => x.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now))
            .Include(x => x.MaintenanceRequests.Where(repair => OpenMaintenanceStatuses.Contains(repair.Status)))
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == roomId, cancellationToken);

    private static PlacementRoomViewModel ToPlacementRoom(Room room, DateTime now)
    {
        var capacity = RoomCapacityPolicy.FromRoom(room, now);
        var (label, css) = capacity.Blocked > 0
            ? ("Đang bảo trì", "maintenance")
            : capacity.Assignable > 0
                ? ($"Còn {capacity.Assignable} chỗ", "available")
                : capacity.Held > 0
                    ? ($"Đang giữ {capacity.Held} chỗ", "held")
                    : ("Đã đầy", "full");
        return new(room.Code, room.Floor.Building.Name, room.Floor.Number, room.RoomType,
            capacity.Capacity, capacity.Occupied, capacity.Held, capacity.Blocked, capacity.Assignable,
            room.MonthlyPrice, label, css);
    }

    private IQueryable<ApplicationProjection> Projections() => db.HousingRequests.AsNoTracking()
        .Select(x => new ApplicationProjection
        {
            Id = x.Id,
            Code = x.Code,
            StudentCode = x.StudentProfile.StudentCode,
            StudentName = x.StudentProfile.FullName,
            Type = x.Type,
            Status = x.Status,
            CurrentRoomCode = x.StudentProfile.Stays.Where(stay => stay.EndedAt == null).Select(stay => stay.Room.Code).FirstOrDefault(),
            TargetRoomCode = x.TargetRoom != null ? x.TargetRoom.Code : null,
            SubmittedAt = x.SubmittedAt,
            ActionRequiredAt = x.ActionRequiredAt,
            ReturnedWithSupplement = x.Status == HousingRequestStatus.Submitted &&
                x.Events.Any(e => e.Status == HousingRequestStatus.NeedsInformation)
        });

    private static IQueryable<ApplicationProjection> ApplyFilters(IQueryable<ApplicationProjection> source, ApplicationQueueQuery query, DateTime overdueAt)
    {
        source = query.Scope switch
        {
            "new" => source.Where(x => x.Status == HousingRequestStatus.Submitted),
            "review" => source.Where(x => x.Status == HousingRequestStatus.UnderReview),
            "info" => source.Where(x => x.Status == HousingRequestStatus.NeedsInformation),
            "completed" => source.Where(x => x.Status == HousingRequestStatus.Approved || x.Status == HousingRequestStatus.Rejected || x.Status == HousingRequestStatus.Cancelled || x.Status == HousingRequestStatus.Expired),
            "all" => source,
            _ => source.Where(x => x.Status == HousingRequestStatus.Submitted || x.Status == HousingRequestStatus.UnderReview || x.Status == HousingRequestStatus.NeedsInformation || x.Status == HousingRequestStatus.AwaitingMoveIn)
        };
        if (query.OverdueOnly)
            source = source.Where(x => (x.Status == HousingRequestStatus.Submitted || x.Status == HousingRequestStatus.UnderReview || x.Status == HousingRequestStatus.AwaitingMoveIn) &&
                                       (x.ActionRequiredAt ?? x.SubmittedAt) <= overdueAt);
        if (Enum.TryParse<HousingRequestType>(query.Type, true, out var type) && Enum.IsDefined(type))
            source = source.Where(x => x.Type == type);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x => x.Code.Contains(search) || x.StudentCode.Contains(search) ||
                                       x.StudentName.Contains(search) || (x.TargetRoomCode != null && x.TargetRoomCode.Contains(search)));
        }
        return source;
    }

    private static string? RecordInventory(HousingRequest request, ApplicationActionInput input)
    {
        var values = new[] { input.Keys, input.Furniture, input.Equipment };
        if (request.AppointmentUtc == null && values.All(string.IsNullOrWhiteSpace)) return null;
        if (values.Any(x => x is not ("Đủ, tốt" or "Hư hỏng" or "Thiếu" or "Không áp dụng")))
            return "Ghi nhận tình trạng chìa khóa, nội thất và thiết bị trước khi xác nhận.";
        request.Inventory = $"Chìa khóa: {input.Keys} · Nội thất: {input.Furniture} · Thiết bị: {input.Equipment}";
        return null;
    }

    private bool TrySetOriginalVersion(HousingRequest request, string rowVersion)
    {
        try
        {
            var version = Convert.FromBase64String(rowVersion);
            if (version.Length == 0) return false;
            db.Entry(request).Property(x => x.RowVersion).OriginalValue = version;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static HousingRequestEvent NewEvent(long requestId, HousingRequestStatus status, string note, string actorUserId, DateTime createdAt) => new()
    {
        HousingRequestId = requestId,
        Status = status,
        Note = note,
        ActorUserId = actorUserId,
        CreatedAt = createdAt
    };

    private void AddNotification(string? userId, string title, string body, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = "Application",
            Title = title,
            Body = body.Length <= 300 ? body : string.Concat(body.AsSpan(0, 297), "..."),
            TargetUrl = "/Student/Applications",
            CreatedAt = createdAt
        });
    }

    private static bool IsManagerWork(HousingRequestStatus status) =>
        status is HousingRequestStatus.Submitted or HousingRequestStatus.UnderReview or HousingRequestStatus.AwaitingMoveIn;

    private static string Initials(string name) => string.Concat(name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .TakeLast(2)
        .Select(x => x[0]));

    private sealed class ApplicationProjection
    {
        public long Id { get; init; }
        public required string Code { get; init; }
        public required string StudentCode { get; init; }
        public required string StudentName { get; init; }
        public HousingRequestType Type { get; init; }
        public HousingRequestStatus Status { get; init; }
        public string? CurrentRoomCode { get; init; }
        public string? TargetRoomCode { get; init; }
        public DateTime SubmittedAt { get; init; }
        public DateTime? ActionRequiredAt { get; init; }
        public bool ReturnedWithSupplement { get; init; }
    }
}
