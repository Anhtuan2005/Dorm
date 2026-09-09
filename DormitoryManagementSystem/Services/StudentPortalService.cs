using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class StudentPortalService(DormitoryDbContext db) : IStudentPortalService
{
    private static readonly HousingRequestStatus[] ActiveRequestStatuses =
        [HousingRequestStatus.Submitted, HousingRequestStatus.UnderReview, HousingRequestStatus.NeedsInformation];

    private static readonly MaintenanceStatus[] OpenRepairStatuses =
        [MaintenanceStatus.New, MaintenanceStatus.Assigned, MaintenanceStatus.InProgress, MaintenanceStatus.WaitingParts];

    public async Task<StudentDashboardViewModel> GetDashboardAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileWithStayAsync(userId, cancellationToken);
        var currentStay = profile.Stays.Single(x => x.EndedAt == null);
        var currentRoomOccupants = await db.Stays.CountAsync(
            x => x.RoomId == currentStay.RoomId && x.EndedAt == null,
            cancellationToken);
        var request = await db.HousingRequests.AsNoTracking()
            .Include(x => x.TargetRoom)
            .Where(x => x.StudentProfileId == profile.Id && ActiveRequestStatuses.Contains(x.Status))
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var invoice = await db.Invoices.AsNoTracking()
            .Include(x => x.BillingPeriod)
            .Include(x => x.Lines)
            .Where(x => x.StudentProfileId == profile.Id && x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Cancelled)
            .OrderBy(x => x.BillingPeriod.DueDate)
            .FirstOrDefaultAsync(cancellationToken);
        var repair = await db.MaintenanceRequests.AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.Updates)
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
            CurrentStay = ToStay(currentStay, currentRoomOccupants),
            ActiveRequest = request is null ? null : ToHousingRequest(request),
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
        var currentRoomId = profile.Stays.Single(x => x.EndedAt == null).RoomId;
        var now = DateTime.UtcNow;
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
            var occupied = room.Stays.Count;
            var blocked = Math.Min(Math.Max(0, room.Capacity - occupied), room.MaintenanceRequests.Sum(x => x.BlockedBeds));
            var held = Math.Min(Math.Max(0, room.Capacity - occupied - blocked), room.Holds.Sum(x => x.HeldBeds));
            return new StudentRoomFact(room, occupied, Math.Max(0, room.Capacity - occupied - blocked - held));
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
            .Where(x => x.StudentProfileId == profile.Id && ActiveRequestStatuses.Contains(x.Status))
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new StudentRoomSearchViewModel
        {
            Student = ToIdentity(profile),
            Query = query,
            CurrentRoomCode = profile.Stays.Single(x => x.EndedAt == null).Room.Code,
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
            ActiveRequest = activeRequest is null ? null : ToHousingRequest(activeRequest)
        };
    }

    public async Task<StudentApplicationsViewModel> GetApplicationsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileAsync(userId, cancellationToken);
        var requests = await db.HousingRequests.AsNoTracking()
            .Include(x => x.TargetRoom)
            .Where(x => x.StudentProfileId == profile.Id)
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync(cancellationToken);

        return new StudentApplicationsViewModel
        {
            Student = ToIdentity(profile),
            Requests = requests.Select(ToHousingRequest).ToList()
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
                x => x.StudentProfileId == profile.Id && ActiveRequestStatuses.Contains(x.Status),
                cancellationToken))
            return new(false, "Bạn đang có một đơn chờ xử lý. Hãy theo dõi hoặc hủy đơn đó trước.");

        var now = DateTime.UtcNow;
        var room = await db.Rooms
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
            .Include(x => x.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now))
            .Include(x => x.MaintenanceRequests.Where(repair => OpenRepairStatuses.Contains(repair.Status)))
            .SingleOrDefaultAsync(x => x.Id == input.TargetRoomId, cancellationToken);
        if (room is null) return new(false, "Phòng đã chọn không tồn tại.");
        if (room.RoomType != profile.Gender) return new(false, "Phòng này không phù hợp chính sách lưu trú trong hồ sơ của bạn.");
        if (profile.Stays.Any(x => x.RoomId == room.Id)) return new(false, "Đây đang là phòng hiện tại của bạn.");

        var blocked = room.MaintenanceRequests.Sum(x => x.BlockedBeds);
        var held = room.Holds.Sum(x => x.HeldBeds);
        var assignable = room.Capacity - room.Stays.Count - blocked - held;
        if (assignable <= 0) return new(false, "Phòng vừa hết chỗ hoặc tạm ngừng nhận đơn. Danh sách đã được cập nhật.");

        var sequence = await db.HousingRequests.CountAsync(cancellationToken) + 1;
        var request = new HousingRequest
        {
            Code = $"DK-{DateTime.UtcNow:yyyy}-{sequence:0000}",
            StudentProfileId = profile.Id,
            TargetRoomId = room.Id,
            Type = profile.Stays.Count > 0 ? HousingRequestType.Transfer : HousingRequestType.Registration,
            Status = HousingRequestStatus.Submitted,
            StudentReason = input.Reason.Trim(),
            SubmittedAt = DateTime.UtcNow,
            Events =
            [
                new HousingRequestEvent
                {
                    Status = HousingRequestStatus.Submitted,
                    Note = "Sinh viên đã gửi yêu cầu.",
                    ActorUserId = userId,
                    CreatedAt = DateTime.UtcNow
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
        long requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await db.HousingRequests
            .Include(x => x.StudentProfile)
            .SingleOrDefaultAsync(x => x.Id == requestId && x.StudentProfile.UserId == userId, cancellationToken);
        if (request is null) return new(false, "Không tìm thấy đơn thuộc tài khoản của bạn.");
        if (!ActiveRequestStatuses.Contains(request.Status)) return new(false, "Chỉ có thể hủy đơn đang chờ xử lý.");

        request.Status = HousingRequestStatus.Cancelled;
        request.ReviewedAt = DateTime.UtcNow;
        request.Events.Add(new HousingRequestEvent
        {
            Status = HousingRequestStatus.Cancelled,
            Note = "Sinh viên đã hủy yêu cầu.",
            ActorUserId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return new(true, $"Đã hủy đơn {request.Code}.");
    }

    public async Task<StudentInvoicesViewModel> GetInvoicesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileAsync(userId, cancellationToken);
        var invoices = await db.Invoices.AsNoTracking()
            .Include(x => x.BillingPeriod)
            .Include(x => x.Lines)
            .Where(x => x.StudentProfileId == profile.Id)
            .OrderByDescending(x => x.BillingPeriod.StartDate)
            .ToListAsync(cancellationToken);

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
        var currentStay = profile.Stays.Single(x => x.EndedAt == null);
        var repairs = await db.MaintenanceRequests.AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.Updates)
            .Where(x => x.ReporterStudentId == profile.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return new StudentRepairsViewModel
        {
            Student = ToIdentity(profile),
            CurrentRoomCode = currentStay.Room.Code,
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Updates =
            [
                new MaintenanceUpdate
                {
                    Status = MaintenanceStatus.New,
                    Note = "Yêu cầu đã được tiếp nhận và đang chờ phân công.",
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        db.MaintenanceRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);
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

    private static StudentHousingRequestViewModel ToHousingRequest(HousingRequest request)
    {
        var (status, css) = request.Status switch
        {
            HousingRequestStatus.Submitted => ("Đã gửi", "submitted"),
            HousingRequestStatus.UnderReview => ("Đang xét duyệt", "reviewing"),
            HousingRequestStatus.NeedsInformation => ("Cần bổ sung", "needs-info"),
            HousingRequestStatus.Approved => ("Đã duyệt", "approved"),
            HousingRequestStatus.Rejected => ("Từ chối", "rejected"),
            _ => ("Đã hủy", "cancelled")
        };
        var type = request.Type switch
        {
            HousingRequestType.Registration => "Đăng ký ở",
            HousingRequestType.Transfer => "Chuyển phòng",
            _ => "Trả phòng"
        };
        return new(
            request.Id,
            request.Code,
            type,
            status,
            css,
            request.TargetRoom?.Code,
            request.StudentReason,
            request.DecisionReason,
            request.SubmittedAt,
            request.ReviewedAt,
            ActiveRequestStatuses.Contains(request.Status));
    }

    private static StudentInvoiceViewModel ToInvoice(Invoice invoice)
    {
        var balance = Math.Max(0, invoice.TotalAmount - invoice.PaidAmount);
        var isOverdue = balance > 0 && invoice.BillingPeriod.DueDate < DateOnly.FromDateTime(DateTime.Today);
        var (label, css) = invoice.Status == InvoiceStatus.Paid || balance == 0
            ? ("Đã thanh toán", "paid")
            : isOverdue
                ? ("Quá hạn", "overdue")
                : invoice.PaidAmount > 0
                    ? ("Thanh toán một phần", "partial")
                    : ("Chờ thanh toán", "issued");
        return new(
            invoice.Id,
            invoice.Code,
            invoice.BillingPeriod.Name,
            invoice.BillingPeriod.DueDate,
            invoice.TotalAmount,
            invoice.PaidAmount,
            balance,
            label,
            css,
            balance > 0,
            invoice.Lines.OrderBy(x => x.Id).Select(x => new StudentInvoiceLineViewModel(x.Description, x.Amount)).ToList());
    }

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
