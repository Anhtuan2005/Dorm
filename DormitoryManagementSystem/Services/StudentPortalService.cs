using System.Data;
using System.Data.Common;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class StudentPortalService(
    DormitoryDbContext db,
    IBillingLedgerService billingLedger,
    IMaintenanceAttachmentStorage attachmentStorage,
    TimeProvider timeProvider,
    RealtimeUpdates? live = null) : IStudentPortalService
{
    private static readonly MaintenanceStatus[] OpenRepairStatuses =
        [MaintenanceStatus.New, MaintenanceStatus.Assigned, MaintenanceStatus.InProgress, MaintenanceStatus.WaitingParts];

    public async Task<StudentNotificationsViewModel> GetNotificationsAsync(string userId, int page, CancellationToken cancellationToken = default)
    {
        const int pageSize = 25;
        var counts = await db.Notifications.Where(x => x.UserId == userId).GroupBy(x => x.UserId)
            .Select(group => new { Total = group.Count(), Unread = group.Count(x => x.ReadAt == null) })
            .SingleOrDefaultAsync(cancellationToken);
        var total = counts?.Total ?? 0;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        return new()
        {
            Notifications = await LoadNotificationFeedAsync(userId, pageSize, cancellationToken, (page - 1) * pageSize),
            UnreadCount = counts?.Unread ?? 0,
            TotalCount = total,
            Page = page,
            TotalPages = totalPages
        };
    }

    public Task<int> GetUnreadNotificationCountAsync(string userId, CancellationToken cancellationToken = default) =>
        db.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && x.ReadAt == null, cancellationToken);

    public Task<IReadOnlyList<StudentNotificationViewModel>> GetRecentNotificationsAsync(string userId, CancellationToken cancellationToken = default) =>
        LoadNotificationFeedAsync(userId, 3, cancellationToken);

    private async Task<IReadOnlyList<StudentNotificationViewModel>> LoadNotificationFeedAsync(
        string userId, int limit, CancellationToken cancellationToken, int skip = 0)
    {
        var notifications = await db.Notifications.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(skip).Take(limit).ToListAsync(cancellationToken);
        return notifications.Select(ToNotification).ToList();
    }

    public async Task<string?> ReadNotificationAsync(string userId, long id, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (notification is null) return null;
        if (notification.ReadAt is null)
        {
            notification.ReadAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
            if (live is not null) await live.NotifyStudentsAsync(userId);
        }
        return SafeLocalUrl(notification.TargetUrl);
    }

    public async Task<int> MarkAllNotificationsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var changed = await db.Notifications.Where(x => x.UserId == userId && x.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAt, now), cancellationToken);
        if (changed != 0 && live is not null) await live.NotifyStudentsAsync(userId);
        return changed;
    }

    public async Task<StudentDashboardViewModel> GetDashboardAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileWithStayAsync(userId, cancellationToken);
        var currentStay = profile.Stays.SingleOrDefault(x => x.EndedAt == null);
        var roommateNames = currentStay is null ? [] : await db.Stays.AsNoTracking()
            .Where(other => other.RoomId == currentStay.RoomId && other.EndedAt == null &&
                other.StudentProfileId != profile.Id &&
                db.Stays.Any(mine => mine.Id == currentStay.Id && mine.RoomId == other.RoomId &&
                    mine.EndedAt == null && mine.StudentProfile.UserId == userId))
            .OrderBy(other => other.StudentProfile.FullName)
            .ThenBy(other => other.StudentProfileId)
            .Select(other => other.StudentProfile.FullName)
            .ToListAsync(cancellationToken);
        var request = await db.HousingRequests.AsNoTracking()
            .Include(x => x.TargetRoom)
            .Where(x => x.StudentProfileId == profile.Id && HousingRequestWorkflow.ActiveStatuses.Contains(x.Status))
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var invoice = (await billingLedger.GetStudentInvoicesAsync(profile.Id, cancellationToken))
            .Where(x => x.Summary.CanCollect)
            .OrderBy(x => x.DueDate)
            .FirstOrDefault();
        var repair = await db.MaintenanceRequests.AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.Updates)
            .Include(x => x.Attachments)
            .Where(x => x.ReporterStudentId == profile.Id && OpenRepairStatuses.Contains(x.Status))
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var notifications = await GetRecentNotificationsAsync(userId, cancellationToken);

        return new StudentDashboardViewModel
        {
            Student = ToIdentity(profile),
            CurrentStay = currentStay is null ? null : ToStay(currentStay, roommateNames),
            ActiveRequest = request is null ? null : ToHousingRequest(request, timeProvider.GetUtcNow().UtcDateTime),
            NextInvoice = invoice is null ? null : ToInvoice(invoice),
            ActiveRepair = repair is null ? null : ToRepair(repair),
            Notifications = notifications
        };
    }

    public async Task<StudentRoomSearchViewModel> SearchRoomsAsync(
        string userId,
        StudentRoomSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileWithStayAsync(userId, cancellationToken);
        var currentStay = profile.Stays.SingleOrDefault(x => x.EndedAt == null);
        var currentRoomId = currentStay?.RoomId;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rooms = await SearchableRooms(profile.Gender, now).ToListAsync(cancellationToken);

        var facts = rooms.Select(room =>
        {
            var capacity = RoomCapacityPolicy.FromRoom(room, now);
            return new StudentRoomFact(room, capacity.Occupied, capacity.Assignable);
        }).Where(x => x.Assignable > 0 && x.Room.Id != currentRoomId).ToList();

        var buildings = facts.Select(x => x.Room.Floor.Building.Code).Distinct().Order().ToList();
        var amenities = facts.SelectMany(x => RoomAmenities.Optional(x.Room.Amenities)).Distinct().Order().ToList();

        IEnumerable<StudentRoomFact> filtered = facts;
        if (!string.IsNullOrWhiteSpace(query.Building))
        {
            query.Building = query.Building.Trim().ToUpperInvariant();
            filtered = filtered.Where(x => x.Room.Floor.Building.Code == query.Building);
        }
        if (query.MaxPrice.HasValue)
            filtered = filtered.Where(x => x.Room.MonthlyPrice <= query.MaxPrice.Value);
        if (!string.IsNullOrWhiteSpace(query.Amenity))
            filtered = filtered.Where(x => RoomAmenities.List(x.Room.Amenities).Contains(query.Amenity));

        var ordered = query.Sort switch
        {
            "price" => filtered.OrderBy(x => x.Room.MonthlyPrice).ThenBy(x => x.Room.Code),
            "building" => filtered.OrderBy(x => x.Room.Floor.Building.SortOrder).ThenBy(x => x.Room.Code),
            _ => filtered.OrderByDescending(x => x.Assignable).ThenBy(x => x.Room.MonthlyPrice).ThenBy(x => x.Room.Code)
        };

        var activeRequest = await db.HousingRequests.AsNoTracking()
            .Include(x => x.TargetRoom)
            .Where(x => x.StudentProfileId == profile.Id && HousingRequestWorkflow.ActiveStatuses.Contains(x.Status))
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);

        const int pageSize = 24;
        var matches = ordered.ToList();
        var totalPages = Math.Max(1, (matches.Count + pageSize - 1) / pageSize);
        query.Page = Math.Clamp(query.Page, 1, totalPages);
        var shownRooms = matches.Skip((query.Page - 1) * pageSize).Take(pageSize).ToList();
        var roomIds = shownRooms.Select(x => x.Room.Id).ToList();
        var roomPhotos = await db.RoomPhotos.AsNoTracking().Where(x => roomIds.Contains(x.RoomId))
            .OrderBy(x => x.Id)
            .Select(x => new { x.RoomId, x.Id, x.Caption }).ToListAsync(cancellationToken);
        var photosByRoom = roomPhotos.ToLookup(x => x.RoomId, x => new RoomPhotoViewModel(x.Id, x.Caption));

        return new StudentRoomSearchViewModel
        {
            Student = ToIdentity(profile),
            Query = query,
            CurrentRoomCode = currentStay?.Room.Code,
            Rooms = shownRooms.Select(x => ToRoomOption(x.Room, x.Occupied, x.Assignable, photosByRoom[x.Room.Id].ToArray())).ToList(),
            Buildings = buildings,
            Amenities = amenities,
            TotalMatches = matches.Count,
            TotalPages = totalPages,
            ActiveRequest = activeRequest is null ? null : ToHousingRequest(activeRequest, now)
        };
    }

    public async Task<StudentRoomPreviewViewModel?> GetRoomPreviewAsync(
        string userId, int roomId, CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileWithStayAsync(userId, cancellationToken);
        var currentStay = profile.Stays.SingleOrDefault(x => x.EndedAt == null);
        if (currentStay?.RoomId == roomId) return null;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var room = await SearchableRooms(profile.Gender, now).SingleOrDefaultAsync(x => x.Id == roomId, cancellationToken);
        if (room is null) return null;

        var photos = await db.RoomPhotos.AsNoTracking().Where(x => x.RoomId == roomId).OrderBy(x => x.Id)
            .Select(x => new RoomPhotoViewModel(x.Id, x.Caption)).ToListAsync(cancellationToken);
        var hasActiveRequest = await db.HousingRequests.AnyAsync(
            x => x.StudentProfileId == profile.Id && HousingRequestWorkflow.ActiveStatuses.Contains(x.Status), cancellationToken);
        var capacity = RoomCapacityPolicy.FromRoom(room, now);
        var available = RoomBedPolicy.Available(room, now);
        return new(ToRoomOption(room, capacity.Occupied, available.Count, photos), available, hasActiveRequest, currentStay is not null);
    }

    private IQueryable<Room> SearchableRooms(string gender, DateTime now) => db.Rooms.AsNoTracking()
        .Include(x => x.Floor).ThenInclude(x => x.Building)
        .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
        .Include(x => x.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now))
        .Include(x => x.MaintenanceRequests.Where(repair => OpenRepairStatuses.Contains(repair.Status)))
        .AsSplitQuery()
        .Where(x => x.RoomType == gender);

    private static StudentRoomOptionViewModel ToRoomOption(Room room, int occupied, int assignable, IReadOnlyList<RoomPhotoViewModel> photos) =>
        new(room.Id, room.Code, room.Floor.Building.Name, room.Floor.Number, room.RoomType, room.MonthlyPrice,
            room.Capacity, occupied, assignable, RoomAmenities.List(room.Amenities), false,
            assignable >= 3 ? "Nhiều chỗ trống" : "Phù hợp hồ sơ")
        {
            Photos = photos
        };

    public async Task<StudentApplicationsViewModel> GetApplicationsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileWithStayAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var requests = await db.HousingRequests.AsNoTracking()
            .Include(x => x.TargetRoom)
            .Include(x => x.Events).ThenInclude(x => x.ActorUser)
            .Where(x => x.StudentProfileId == profile.Id)
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync(cancellationToken);

        return new StudentApplicationsViewModel
        {
            Student = ToIdentity(profile),
            Requests = requests.Select(x => ToHousingRequest(x, now)).ToList(),
            CurrentRoomCode = profile.Stays.SingleOrDefault()?.Room.Code,
            HasActiveRequest = requests.Any(x => HousingRequestWorkflow.IsActive(x.Status))
        };
    }

    public async Task<StudentOperationResult> SubmitHousingRequestAsync(
        string userId,
        SubmitHousingRequestInput input,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.StudentProfiles
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null) return new(false, "Không tìm thấy hồ sơ sinh viên của tài khoản này.");

        if (await db.HousingRequests.AnyAsync(
                x => x.StudentProfileId == profile.Id && HousingRequestWorkflow.ActiveStatuses.Contains(x.Status),
                cancellationToken))
            return new(false, "Bạn đang có một đơn chờ xử lý. Hãy theo dõi hoặc hủy đơn đó trước.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var room = await db.Rooms
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
            .Include(x => x.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now))
            .Include(x => x.MaintenanceRequests.Where(repair => OpenRepairStatuses.Contains(repair.Status)))
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == input.TargetRoomId, cancellationToken);
        if (room is null) return new(false, "Phòng đã chọn không tồn tại.");
        if (!string.Equals(room.RoomType, profile.Gender, StringComparison.OrdinalIgnoreCase))
            return new(false, "Phòng này không phù hợp chính sách lưu trú trong hồ sơ của bạn.");
        if (profile.Stays.Any(x => x.RoomId == room.Id)) return new(false, "Đây đang là phòng hiện tại của bạn.");

        var capacity = RoomCapacityPolicy.FromRoom(room, now);
        if (!capacity.CanAssign) return new(false, $"{capacity.AvailabilityReason}. Danh sách đã được cập nhật.");
        if (!RoomBedPolicy.Available(room, now).Contains(input.TargetBedNumber))
            return new(false, "Giường đã chọn hiện không còn có thể xếp. Vui lòng chọn giường khác.");

        var code = await NextHousingRequestCodeAsync(now, cancellationToken);
        var currentStay = profile.Stays.SingleOrDefault();
        var request = new HousingRequest
        {
            Code = code,
            StudentProfileId = profile.Id,
            TargetRoomId = room.Id,
            TargetBedNumber = input.TargetBedNumber,
            SourceStayId = currentStay?.Id,
            Type = currentStay is null ? HousingRequestType.Registration : HousingRequestType.Transfer,
            Status = HousingRequestStatus.Submitted,
            StudentReason = input.Reason.Trim(),
            SubmittedAt = now,
            ActionRequiredAt = now,
            Events =
            [
                new HousingRequestEvent
                {
                    Status = HousingRequestStatus.Submitted,
                    Note = "Sinh viên đã gửi yêu cầu.",
                    ActorUserId = userId,
                    CreatedAt = now
                }
            ]
        };
        db.HousingRequests.Add(request);

        try
        {
            await OperationsNotifications.StageApplicationAsync(db, request, "Sinh viên gửi đơn lưu trú mới", now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (live is not null) await live.NotifyOperationsAsync();
            return new(true, $"Đã gửi đơn {request.Code} cho phòng {room.Code}. Ban quản lý sẽ phản hồi trên trang Đăng ký của tôi.");
        }
        catch (DbUpdateException)
        {
            return new(false, "Không thể gửi đơn do dữ liệu vừa thay đổi. Vui lòng tải lại và thử lại.");
        }
    }

    public async Task<StudentOperationResult> CancelHousingRequestAsync(
        string userId,
        StudentHousingRequestActionInput input,
        CancellationToken cancellationToken = default)
    {
        var request = await db.HousingRequests
            .Include(x => x.StudentProfile)
            .Include(x => x.ApprovedRoomHold)
            .SingleOrDefaultAsync(x => x.Id == input.Id && x.StudentProfile.UserId == userId, cancellationToken);
        if (request is null) return new(false, "Không tìm thấy đơn thuộc tài khoản của bạn.");
        if (!HasCurrentVersion(request, input.RowVersion)) return new(false, "Đơn vừa được cập nhật. Hãy tải lại trước khi hủy.");
        var transition = HousingRequestWorkflow.Transition(request.Status, HousingRequestAction.Cancel);
        if (!transition.Allowed) return new(false, transition.ErrorMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        request.Status = transition.NextStatus;
        request.ActionRequiredAt = null;
        request.ReviewedAt = now;
        if (request.ApprovedRoomHold is not null && request.ApprovedRoomHold.ReleasedAt is null)
            request.ApprovedRoomHold.ReleasedAt = now;
        request.Events.Add(new HousingRequestEvent
        {
            Status = HousingRequestStatus.Cancelled,
            Note = "Sinh viên đã hủy yêu cầu.",
            ActorUserId = userId,
            CreatedAt = now
        });
        try
        {
            await OperationsNotifications.StageApplicationAsync(db, request, "Sinh viên đã hủy đơn lưu trú", now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (live is not null) await live.NotifyOperationsAsync();
            return new(true, $"Đã hủy đơn {request.Code}.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Đơn vừa được Ban quản lý xử lý. Hãy tải lại để xem trạng thái mới.");
        }
    }

    public async Task<StudentOperationResult> SupplementHousingRequestAsync(
        string userId,
        SupplementHousingRequestInput input,
        CancellationToken cancellationToken = default)
    {
        var request = await db.HousingRequests
            .Include(x => x.StudentProfile)
            .SingleOrDefaultAsync(x => x.Id == input.Id && x.StudentProfile.UserId == userId, cancellationToken);
        if (request is null) return new(false, "Không tìm thấy đơn thuộc tài khoản của bạn.");
        if (!HasCurrentVersion(request, input.RowVersion)) return new(false, "Đơn vừa được cập nhật. Hãy tải lại trước khi gửi bổ sung.");
        var transition = HousingRequestWorkflow.Transition(request.Status, HousingRequestAction.SubmitInformation);
        if (!transition.Allowed) return new(false, transition.ErrorMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        request.Status = transition.NextStatus;
        request.ActionRequiredAt = now;
        request.Events.Add(new HousingRequestEvent
        {
            Status = transition.NextStatus,
            Note = input.Information.Trim(),
            ActorUserId = userId,
            CreatedAt = now
        });
        try
        {
            await OperationsNotifications.StageApplicationAsync(db, request, "Sinh viên đã bổ sung đơn lưu trú", now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (live is not null) await live.NotifyHousingSupplementAsync(request.Id, request.Code);
            return new(true, $"Đã gửi bổ sung cho đơn {request.Code}. Đơn đã trở lại hàng đợi của Ban quản lý.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Đơn vừa được cập nhật ở phiên khác. Hãy tải lại và kiểm tra trước khi gửi.");
        }
    }

    public async Task<StudentOperationResult> SubmitCheckoutRequestAsync(
        string userId,
        SubmitCheckoutRequestInput input,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.StudentProfiles
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null) return new(false, "Không tìm thấy hồ sơ sinh viên của tài khoản này.");
        var currentStay = profile.Stays.SingleOrDefault();
        if (currentStay is null) return new(false, "Bạn không có lượt lưu trú hiện tại để trả phòng.");
        if (await db.HousingRequests.AnyAsync(
                x => x.StudentProfileId == profile.Id && HousingRequestWorkflow.ActiveStatuses.Contains(x.Status),
                cancellationToken))
            return new(false, "Bạn đang có một đơn chờ xử lý. Hãy hoàn tất hoặc hủy đơn đó trước.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var request = new HousingRequest
        {
            Code = await NextHousingRequestCodeAsync(now, cancellationToken),
            StudentProfileId = profile.Id,
            SourceStayId = currentStay.Id,
            Type = HousingRequestType.Checkout,
            Status = HousingRequestStatus.Submitted,
            StudentReason = input.Reason.Trim(),
            SubmittedAt = now,
            ActionRequiredAt = now,
            Events =
            [
                new HousingRequestEvent
                {
                    Status = HousingRequestStatus.Submitted,
                    Note = "Sinh viên đã gửi yêu cầu trả phòng.",
                    ActorUserId = userId,
                    CreatedAt = now
                }
            ]
        };
        db.HousingRequests.Add(request);
        try
        {
            await OperationsNotifications.StageApplicationAsync(db, request, "Sinh viên gửi đơn trả phòng", now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (live is not null) await live.NotifyOperationsAsync();
            return new(true, $"Đã gửi đơn {request.Code} yêu cầu trả phòng. Lượt lưu trú chỉ kết thúc sau khi Ban quản lý duyệt.");
        }
        catch (DbUpdateException)
        {
            return new(false, "Không thể gửi đơn do dữ liệu vừa thay đổi. Vui lòng tải lại và thử lại.");
        }
    }

    public async Task<StudentInvoicesViewModel> GetInvoicesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileAsync(userId, cancellationToken);
        var invoices = await billingLedger.GetStudentInvoicesAsync(profile.Id, cancellationToken);
        var activeInvoices = invoices.Where(invoice => invoice.Summary.State != BillingPaymentState.Cancelled).ToList();

        return new StudentInvoicesViewModel
        {
            Student = ToIdentity(profile),
            Invoices = invoices.Select(invoice =>
            {
                var view = ToInvoice(invoice);
                var previous = invoice.Summary.State == BillingPaymentState.Cancelled
                    ? null : activeInvoices.FirstOrDefault(candidate => candidate.PeriodEnd < invoice.PeriodStart);
                return previous is null ? view : view with { Comparison = StudentInvoiceComparisonBuilder.Compare(invoice, previous) };
            }).ToList()
        };
    }

    public async Task<StudentRepairsViewModel> GetRepairsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileWithStayAsync(userId, cancellationToken);
        var currentStay = profile.Stays.SingleOrDefault(x => x.EndedAt == null);
        var repairs = await db.MaintenanceRequests.AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.Updates)
            .Include(x => x.Attachments)
            .Where(x => x.ReporterStudentId == profile.Id)
            .OrderByDescending(x => x.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new StudentRepairsViewModel
        {
            Student = ToIdentity(profile),
            CurrentRoomCode = currentStay?.Room.Code ?? "Chưa xếp phòng",
            CanCreateRepair = currentStay is not null,
            Repairs = repairs.Select(ToRepair).ToList(),
            SharedRepairs = currentStay is null ? [] : await LoadSharedRepairsAsync(currentStay, cancellationToken)
        };
    }

    private async Task<IReadOnlyList<SharedStudentRepair>> LoadSharedRepairsAsync(Stay stay, CancellationToken cancellationToken)
    {
        var rows = await db.MaintenanceRequests.AsNoTracking()
            .Where(x => x.RoomId == stay.RoomId && x.ShareWithRoom && x.ReporterStudentId != stay.StudentProfileId)
            .Select(x => new
            {
                x.Id, x.Code, x.Title, x.Category, x.Status, x.UpdatedAt,
                IsFollowing = x.Followers.Any(follower => follower.StayId == stay.Id)
            })
            .Where(x => OpenRepairStatuses.Contains(x.Status) || x.IsFollowing)
            .OrderByDescending(x => x.IsFollowing)
            .ThenByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            var (status, css) = StudentRepairStatus(row.Status);
            return new SharedStudentRepair(row.Id, row.Code, row.Title, row.Category, status, css, row.UpdatedAt, row.IsFollowing);
        }).ToList();
    }

    public Task<StudentOperationResult> SetRepairFollowingAsync(
        string userId, int requestId, bool following, CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var stayId = await (from stay in db.Stays
                                from request in db.MaintenanceRequests
                                where stay.StudentProfile.UserId == userId && stay.EndedAt == null
                                    && request.Id == requestId && request.RoomId == stay.RoomId && request.ShareWithRoom
                                    && request.ReporterStudentId != stay.StudentProfileId
                                    && (!following || OpenRepairStatuses.Contains(request.Status))
                                select (int?)stay.Id).SingleOrDefaultAsync(cancellationToken);
            if (stayId is null) return new StudentOperationResult(false, "Sự cố này hiện không thể theo dõi từ phòng của bạn.");

            var subscription = db.MaintenanceFollowers.Where(x => x.MaintenanceRequestId == requestId && x.StayId == stayId.Value);
            if (!following)
            {
                await subscription.ExecuteDeleteAsync(cancellationToken);
            }
            else if (!await subscription.AnyAsync(cancellationToken))
            {
                var follower = new MaintenanceFollower { MaintenanceRequestId = requestId, StayId = stayId.Value };
                db.MaintenanceFollowers.Add(follower);
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    db.Entry(follower).State = EntityState.Detached;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return following
                ? new(true, "Bạn sẽ nhận thông báo khi sự cố được cập nhật.")
                : new(true, "Bạn đã ngừng theo dõi sự cố này.");
        });
    }

    public async Task<StudentOperationResult> CreateRepairAsync(
        string userId,
        CreateRepairRequestInput input,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.StudentProfiles
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null) return new(false, "Không tìm thấy hồ sơ sinh viên của tài khoản này.");
        var stay = profile.Stays.SingleOrDefault();
        if (stay is null) return new(false, "Bạn chưa có phòng hiện tại để gửi yêu cầu sửa chữa.");

        var priority = Enum.TryParse<MaintenancePriority>(input.Priority, true, out var parsedPriority)
            ? parsedPriority
            : MaintenancePriority.Medium;
        var sequence = await db.MaintenanceRequests.CountAsync(cancellationToken) + 1;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var prepared = await attachmentStorage.PrepareAsync(input.Attachments, now, cancellationToken);
        if (!prepared.Success) return new(false, prepared.Message);
        var request = new MaintenanceRequest
        {
            RoomId = stay.RoomId,
            ReporterStudentId = profile.Id,
            ShareWithRoom = input.ShareWithRoom,
            Code = $"SC-SV-{sequence:0000}",
            Title = input.Title.Trim(),
            Category = input.Category.Trim(),
            Description = input.Description.Trim(),
            Priority = priority,
            Status = MaintenanceStatus.New,
            CreatedAt = now,
            UpdatedAt = now,
            Attachments = prepared.Attachments.ToList(),
            Updates =
            [
                new MaintenanceUpdate
                {
                    Status = MaintenanceStatus.New,
                    Note = "Yêu cầu đã được tiếp nhận và đang chờ phân công.",
                    CreatedAt = now
                }
            ]
        };
        db.MaintenanceRequests.Add(request);
        try
        {
            await OperationsNotifications.StageMaintenanceAsync(db, request, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await attachmentStorage.DeleteAsync(prepared.Attachments);
            throw;
        }
        if (live is not null) await live.NotifyOperationsAsync();
        return new(true, $"Đã gửi yêu cầu {request.Code}. Bạn có thể theo dõi tiến độ ngay tại trang này.");
    }

    private async Task<StudentProfile> LoadProfileAsync(string userId, CancellationToken cancellationToken)
    {
        return await db.StudentProfiles.AsNoTracking()
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Tài khoản chưa được liên kết với hồ sơ sinh viên.");
    }

    private async Task<StudentProfile> LoadProfileWithStayAsync(string userId, CancellationToken cancellationToken)
    {
        return await db.StudentProfiles.AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
                .ThenInclude(x => x.Room)
                    .ThenInclude(x => x.Floor)
                        .ThenInclude(x => x.Building)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Tài khoản chưa được liên kết với hồ sơ sinh viên.");
    }

    private static StudentIdentityViewModel ToIdentity(StudentProfile profile)
    {
        var initials = string.Concat(profile.FullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(2)
            .Select(x => x[0]));
        return new(
            profile.FullName,
            profile.StudentCode,
            profile.Faculty,
            profile.PhoneNumber,
            profile.User?.Email ?? string.Empty,
            initials);
    }

    private static StudentStayViewModel ToStay(Stay stay, IReadOnlyList<string> roommateNames) => new(
        stay.Room.Code,
        stay.Room.Floor.Building.Name,
        stay.Room.Floor.Number,
        stay.StartedAt,
        roommateNames,
        stay.Room.Capacity,
        stay.Room.MonthlyPrice,
        RoomAmenities.List(stay.Room.Amenities))
    {
        BedNumber = stay.BedNumber
    };

    private static StudentHousingRequestViewModel ToHousingRequest(HousingRequest request, DateTime now)
    {
        var workflow = HousingRequestWorkflow.Describe(
            request.Status,
            request.ActionRequiredAt ?? request.SubmittedAt,
            now);
        var events = request.Events.OrderByDescending(x => x.CreatedAt)
            .Select(x =>
            {
                var presentation = HousingRequestWorkflow.Presentation(x.Status);
                return new StudentHousingRequestEventViewModel(
                    presentation.Label,
                    presentation.Css,
                    x.Note,
                    x.ActorUser?.FullName ?? (x.ActorUserId is null ? "Hệ thống" : "Sinh viên"),
                    x.CreatedAt);
            }).ToList();
        return new(
            request.Id,
            request.Code,
            Convert.ToBase64String(request.RowVersion),
            HousingRequestWorkflow.TypeLabel(request.Type),
            workflow.StatusLabel,
            workflow.StatusClass,
            request.TargetRoom?.Code,
            request.StudentReason,
            request.DecisionReason,
            request.SubmittedAt,
            request.ReviewedAt,
            workflow.ResponsibilityLabel,
            workflow.NextActionLabel,
            workflow.WaitingHours,
            HousingRequestWorkflow.Can(request.Status, HousingRequestAction.Cancel),
            HousingRequestWorkflow.Can(request.Status, HousingRequestAction.SubmitInformation),
            events)
        {
            TargetBedNumber = request.TargetBedNumber
        };
    }

    private async Task<string> NextHousingRequestCodeAsync(DateTime now, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT NEXT VALUE FOR [dbo].[HousingRequestNumbers]";
        var value = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return $"DK-{now.AddHours(7):yyyy}-{value:0000}";
    }

    private static bool HasCurrentVersion(HousingRequest request, string rowVersion)
    {
        try
        {
            var version = Convert.FromBase64String(rowVersion);
            return version.Length > 0 && request.RowVersion.SequenceEqual(version);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static StudentInvoiceViewModel ToInvoice(BillingInvoiceSnapshot invoice) => new(
        invoice.Id,
        invoice.Code,
        invoice.PeriodName,
        invoice.DueDate,
        invoice.Summary.Total,
        invoice.Summary.Collected,
        invoice.Summary.Balance,
        invoice.Summary.StatusLabel,
        invoice.Summary.StatusClass,
        invoice.Summary.CanCollect,
        invoice.Lines.Select(x => new StudentInvoiceLineViewModel(x.Description, x.Amount)).ToList(),
        invoice.Transactions.Select(x => new StudentInvoiceTransactionViewModel(
            x.Code,
            x.Amount,
            x.MethodLabel,
            x.ReceivedAt,
            x.IsReversal,
            x.Note) { Id = x.Id, HasBeenReversed = x.HasBeenReversed }).ToList());

    private static (string Label, string Css) StudentRepairStatus(MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.New => ("Đã tiếp nhận", "new"),
        MaintenanceStatus.Assigned => ("Đã phân công", "assigned"),
        MaintenanceStatus.InProgress => ("Đang xử lý", "progress"),
        MaintenanceStatus.WaitingParts => ("Chờ vật tư", "waiting"),
        MaintenanceStatus.Resolved => ("Đã hoàn thành", "resolved"),
        _ => ("Đã đóng", "closed")
    };

    private static StudentRepairViewModel ToRepair(MaintenanceRequest repair)
    {
        var (status, css) = StudentRepairStatus(repair.Status);
        var priority = repair.Priority switch
        {
            MaintenancePriority.Urgent => "Khẩn cấp",
            MaintenancePriority.High => "Ảnh hưởng lớn",
            MaintenancePriority.Medium => "Ảnh hưởng vừa",
            _ => "Ảnh hưởng nhẹ"
        };
        return new(
            repair.Id,
            repair.Code,
            repair.Room.Code,
            repair.Title,
            repair.Category,
            priority,
            status,
            css,
            repair.Description,
            repair.CreatedAt,
            repair.UpdatedAt,
            repair.Updates.OrderBy(x => x.CreatedAt)
                .Select(x => new StudentRepairUpdateViewModel(RepairStatusLabel(x.Status), x.Note, x.CreatedAt))
                .ToList(),
            repair.Attachments.OrderBy(x => x.CreatedAt)
                .Select(x => new MaintenanceAttachmentViewModel(x.Id, x.OriginalName, x.ContentType))
                .ToList()) { ShareWithRoom = repair.ShareWithRoom };
    }

    private static StudentNotificationViewModel ToNotification(Notification notification) => new(
        notification.Id,
        notification.Type,
        notification.Title,
        notification.Body,
        notification.TargetUrl,
        notification.CreatedAt,
        notification.ReadAt is null);

    private static string SafeLocalUrl(string targetUrl) =>
        !string.IsNullOrWhiteSpace(targetUrl) && targetUrl.StartsWith('/') && !targetUrl.StartsWith("//", StringComparison.Ordinal)
            ? targetUrl
            : "/Student/Dashboard";

    private static string RepairStatusLabel(MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.New => "Tiếp nhận",
        MaintenanceStatus.Assigned => "Phân công",
        MaintenanceStatus.InProgress => "Đang xử lý",
        MaintenanceStatus.WaitingParts => "Chờ vật tư",
        MaintenanceStatus.Resolved => "Hoàn thành",
        _ => "Đóng yêu cầu"
    };

    private sealed record StudentRoomFact(Room Room, int Occupied, int Assignable);
}
