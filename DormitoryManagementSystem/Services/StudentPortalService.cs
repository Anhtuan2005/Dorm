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
    TimeProvider timeProvider) : IStudentPortalService
{
    private static readonly MaintenanceStatus[] OpenRepairStatuses =
        [MaintenanceStatus.New, MaintenanceStatus.Assigned, MaintenanceStatus.InProgress, MaintenanceStatus.WaitingParts];

    public async Task<StudentNotificationsViewModel> GetNotificationsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileAsync(userId, cancellationToken);
        var notifications = await db.Notifications.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt).Take(80).ToListAsync(cancellationToken);
        return new StudentNotificationsViewModel { Student = ToIdentity(profile), Notifications = notifications.Select(ToNotification).ToList() };
    }

    public async Task<string?> ReadNotificationAsync(string userId, long id, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (notification is null) return null;
        notification.ReadAt ??= timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return SafeLocalUrl(notification.TargetUrl);
    }

    public async Task<int> MarkAllNotificationsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await db.Notifications.Where(x => x.UserId == userId && x.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAt, now), cancellationToken);
    }

    public async Task<StudentDashboardViewModel> GetDashboardAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileWithStayAsync(userId, cancellationToken);
        var currentStay = profile.Stays.SingleOrDefault(x => x.EndedAt == null);
        var currentRoomOccupants = currentStay is null ? 0 : await db.Stays.CountAsync(
            x => x.RoomId == currentStay.RoomId && x.EndedAt == null,
            cancellationToken);
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
        var notifications = await db.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(4)
            .ToListAsync(cancellationToken);

        return new StudentDashboardViewModel
        {
            Student = ToIdentity(profile),
            CurrentStay = currentStay is null ? null : ToStay(currentStay, currentRoomOccupants),
            ActiveRequest = request is null ? null : ToHousingRequest(request, timeProvider.GetUtcNow().UtcDateTime),
            NextInvoice = invoice is null ? null : ToInvoice(invoice),
            ActiveRepair = repair is null ? null : ToRepair(repair),
            Notifications = notifications.Select(ToNotification).ToList()
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
        var rooms = await db.Rooms.AsNoTracking()
            .Include(x => x.Floor).ThenInclude(x => x.Building)
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
            .Include(x => x.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now))
            .Include(x => x.MaintenanceRequests.Where(repair => OpenRepairStatuses.Contains(repair.Status)))
            .AsSplitQuery()
            .Where(x => x.RoomType == profile.Gender)
            .ToListAsync(cancellationToken);

        var facts = rooms.Select(room =>
        {
            var capacity = RoomCapacityPolicy.FromRoom(room, now);
            return new StudentRoomFact(room, capacity.Occupied, capacity.Assignable);
        }).Where(x => x.Assignable > 0 && x.Room.Id != currentRoomId).ToList();

        var buildings = facts.Select(x => x.Room.Floor.Building.Code).Distinct().Order().ToList();
        var amenities = facts.SelectMany(x => SplitAmenities(x.Room.Amenities)).Distinct().Order().ToList();

        IEnumerable<StudentRoomFact> filtered = facts;
        if (!string.IsNullOrWhiteSpace(query.Building))
            filtered = filtered.Where(x => x.Room.Floor.Building.Code == query.Building.ToUpperInvariant());
        if (query.MaxPrice.HasValue)
            filtered = filtered.Where(x => x.Room.MonthlyPrice <= query.MaxPrice.Value);
        if (!string.IsNullOrWhiteSpace(query.Amenity))
            filtered = filtered.Where(x => SplitAmenities(x.Room.Amenities).Contains(query.Amenity));

        filtered = query.Sort switch
        {
            "price" => filtered.OrderBy(x => x.Room.MonthlyPrice).ThenBy(x => x.Room.Code),
            "building" => filtered.OrderBy(x => x.Room.Floor.Building.SortOrder).ThenBy(x => x.Room.Code),
            _ => filtered.OrderByDescending(x => x.Assignable).ThenBy(x => x.Room.MonthlyPrice)
        };

        var activeRequest = await db.HousingRequests.AsNoTracking()
            .Include(x => x.TargetRoom)
            .Where(x => x.StudentProfileId == profile.Id && HousingRequestWorkflow.ActiveStatuses.Contains(x.Status))
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new StudentRoomSearchViewModel
        {
            Student = ToIdentity(profile),
            Query = query,
            CurrentRoomCode = currentStay?.Room.Code,
            Rooms = filtered.Select(x => new StudentRoomOptionViewModel(
                x.Room.Id,
                x.Room.Code,
                x.Room.Floor.Building.Name,
                x.Room.Floor.Number,
                x.Room.RoomType,
                x.Room.MonthlyPrice,
                x.Room.Capacity,
                x.Occupied,
                x.Assignable,
                SplitAmenities(x.Room.Amenities),
                false,
                x.Assignable >= 3 ? "Nhiều chỗ trống" : "Phù hợp hồ sơ"))
                .Take(24)
                .ToList(),
            Buildings = buildings,
            Amenities = amenities,
            ActiveRequest = activeRequest is null ? null : ToHousingRequest(activeRequest, now)
        };
    }

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
        if (room.RoomType != profile.Gender) return new(false, "Phòng này không phù hợp chính sách lưu trú trong hồ sơ của bạn.");
        if (profile.Stays.Any(x => x.RoomId == room.Id)) return new(false, "Đây đang là phòng hiện tại của bạn.");

        var capacity = RoomCapacityPolicy.FromRoom(room, now);
        if (!capacity.CanAssign) return new(false, $"{capacity.AvailabilityReason}. Danh sách đã được cập nhật.");

        var code = await NextHousingRequestCodeAsync(now, cancellationToken);
        var currentStay = profile.Stays.SingleOrDefault();
        var request = new HousingRequest
        {
            Code = code,
            StudentProfileId = profile.Id,
            TargetRoomId = room.Id,
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
            await db.SaveChangesAsync(cancellationToken);
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
            await db.SaveChangesAsync(cancellationToken);
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
            Note = $"Sinh viên bổ sung: {input.Information.Trim()}",
            ActorUserId = userId,
            CreatedAt = now
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
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
            await db.SaveChangesAsync(cancellationToken);
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

        return new StudentInvoicesViewModel
        {
            Student = ToIdentity(profile),
            Invoices = invoices.Select(ToInvoice).ToList()
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
            Repairs = repairs.Select(ToRepair).ToList()
        };
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
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await attachmentStorage.DeleteAsync(prepared.Attachments);
            throw;
        }
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

    private static StudentStayViewModel ToStay(Stay stay, int currentRoomOccupants) => new(
        stay.Room.Code,
        stay.Room.Floor.Building.Name,
        stay.Room.Floor.Number,
        stay.StartedAt,
        Math.Max(0, currentRoomOccupants - 1),
        stay.Room.Capacity,
        stay.Room.MonthlyPrice,
        SplitAmenities(stay.Room.Amenities));

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
            events);
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
            x.Note)).ToList());

    private static StudentRepairViewModel ToRepair(MaintenanceRequest repair)
    {
        var (status, css) = repair.Status switch
        {
            MaintenanceStatus.New => ("Đã tiếp nhận", "new"),
            MaintenanceStatus.Assigned => ("Đã phân công", "assigned"),
            MaintenanceStatus.InProgress => ("Đang xử lý", "progress"),
            MaintenanceStatus.WaitingParts => ("Chờ vật tư", "waiting"),
            MaintenanceStatus.Resolved => ("Đã hoàn thành", "resolved"),
            _ => ("Đã đóng", "closed")
        };
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
                .ToList());
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

    private static IReadOnlyList<string> SplitAmenities(string amenities) =>
        amenities.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed record StudentRoomFact(Room Room, int Occupied, int Assignable);
}
