using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class StudentManagementService(
    DormitoryDbContext db,
    UserManager<ApplicationUser> userManager,
    IBillingLedgerService billingLedger) : IStudentManagementService
{
    private const int PageSize = 24;

    public async Task<StudentDirectoryViewModel> GetDirectoryAsync(
        StudentDirectoryQuery query,
        CancellationToken cancellationToken = default)
    {
        query.Page = Math.Max(1, query.Page);
        var baseQuery = db.StudentProfiles.AsNoTracking();
        var linkedCount = await baseQuery.CountAsync(x => x.UserId != null, cancellationToken);
        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var buildings = await db.Buildings.AsNoTracking().OrderBy(x => x.SortOrder).Select(x => x.Code).ToListAsync(cancellationToken);

        var filtered = baseQuery
            .Include(x => x.User)
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
                .ThenInclude(x => x.Room).ThenInclude(x => x.Floor).ThenInclude(x => x.Building)
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(x =>
                x.StudentCode.Contains(search) ||
                x.FullName.Contains(search) ||
                (x.User != null && x.User.Email != null && x.User.Email.Contains(search)) ||
                x.Stays.Any(stay => stay.EndedAt == null && stay.Room.Code.Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(query.Building))
            filtered = filtered.Where(x => x.Stays.Any(stay => stay.EndedAt == null && stay.Room.Floor.Building.Code == query.Building));
        filtered = query.Account switch
        {
            "linked" => filtered.Where(x => x.UserId != null),
            "unlinked" => filtered.Where(x => x.UserId == null),
            "locked" => filtered.Where(x => x.User != null && x.User.LockoutEnd > DateTimeOffset.UtcNow),
            _ => filtered
        };

        var filteredCount = await filtered.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)filteredCount / PageSize));
        query.Page = Math.Min(query.Page, totalPages);
        var listed = await filtered.OrderBy(x => x.StudentCode)
            .Skip((query.Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);
        var selected = listed.FirstOrDefault(x => x.Id == query.SelectedId);
        if (selected is null && query.SelectedId.HasValue)
        {
            selected = await baseQuery
                .Include(x => x.User)
                .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
                    .ThenInclude(x => x.Room).ThenInclude(x => x.Floor).ThenInclude(x => x.Building)
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.Id == query.SelectedId.Value, cancellationToken);
        }
        selected ??= listed.FirstOrDefault();
        var items = listed.Select(student =>
        {
            var currentStay = student.Stays.SingleOrDefault();
            return new StudentDirectoryItemViewModel(
                student.Id,
                student.StudentCode,
                student.FullName,
                Initials(student.FullName),
                string.IsNullOrWhiteSpace(student.Faculty) ? "Chưa cập nhật khoa" : student.Faculty,
                currentStay?.Room.Code,
                student.User?.Email,
                student.UserId is not null,
                student.User?.LockoutEnd > DateTimeOffset.UtcNow,
                student.Id == selected?.Id);
        }).ToList();

        return new StudentDirectoryViewModel
        {
            Query = query,
            Items = items,
            Buildings = buildings,
            SelectedStudent = selected is null ? null : await ToDetailAsync(selected, cancellationToken),
            LinkedAccountCount = linkedCount,
            UnlinkedAccountCount = totalCount - linkedCount,
            TotalItems = filteredCount,
            PageSize = PageSize
        };
    }

    public async Task<StudentManagementResult> CreateProfileAsync(
        CreateStudentProfileInput input,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(input.StudentCode);
        if (await db.StudentProfiles.AnyAsync(x => x.StudentCode == code, cancellationToken))
            return new(false, $"Mã sinh viên {code} đã tồn tại.");

        var profile = new StudentProfile
        {
            StudentCode = code,
            FullName = input.FullName.Trim(),
            Gender = input.Gender.Trim(),
            Faculty = input.Faculty.Trim(),
            PhoneNumber = input.PhoneNumber.Trim()
        };
        db.StudentProfiles.Add(profile);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(true, $"Đã tạo hồ sơ {profile.StudentCode} — {profile.FullName}.", profile.Id);
        }
        catch (DbUpdateException)
        {
            return new(false, "Không thể tạo hồ sơ vì mã sinh viên vừa được sử dụng.");
        }
    }

    public async Task<StudentManagementResult> UpdateProfileAsync(
        UpdateStudentProfileInput input,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.StudentProfiles.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken);
        if (profile is null) return new(false, "Không tìm thấy hồ sơ sinh viên.", input.Id);
        var code = NormalizeCode(input.StudentCode);
        if (await db.StudentProfiles.AnyAsync(x => x.Id != input.Id && x.StudentCode == code, cancellationToken))
            return new(false, $"Mã sinh viên {code} đã thuộc hồ sơ khác.", input.Id);

        profile.StudentCode = code;
        profile.FullName = input.FullName.Trim();
        profile.Gender = input.Gender.Trim();
        profile.Faculty = input.Faculty.Trim();
        profile.PhoneNumber = input.PhoneNumber.Trim();
        if (profile.User is not null) profile.User.FullName = profile.FullName;
        await db.SaveChangesAsync(cancellationToken);
        return new(true, $"Đã cập nhật hồ sơ {profile.StudentCode}.", profile.Id);
    }

    public async Task<StudentManagementResult> CreateAccountAsync(
        CreateStudentAccountInput input,
        CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => CreateAccountWithinTransactionAsync(input, cancellationToken));
    }

    private async Task<StudentManagementResult> CreateAccountWithinTransactionAsync(
        CreateStudentAccountInput input,
        CancellationToken cancellationToken)
    {
        var profile = await db.StudentProfiles.SingleOrDefaultAsync(x => x.Id == input.StudentId, cancellationToken);
        if (profile is null) return new(false, "Không tìm thấy hồ sơ sinh viên.");
        if (profile.UserId is not null) return new(false, "Hồ sơ này đã được liên kết với một tài khoản.", profile.Id);
        if (await userManager.FindByEmailAsync(input.Email.Trim()) is not null)
            return new(false, "Email đã được sử dụng bởi tài khoản khác.", profile.Id);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser
        {
            UserName = input.Email.Trim(),
            Email = input.Email.Trim(),
            EmailConfirmed = true,
            FullName = profile.FullName,
            LockoutEnabled = true
        };
        var created = await userManager.CreateAsync(user, input.TemporaryPassword);
        if (!created.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, string.Join(" ", created.Errors.Select(x => x.Description)), profile.Id);
        }
        var roleResult = await userManager.AddToRoleAsync(user, RoleNames.Student);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, "Không thể gán vai trò Sinh viên cho tài khoản.", profile.Id);
        }

        profile.UserId = user.Id;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, $"Đã cấp tài khoản {user.Email} cho {profile.FullName}.", profile.Id);
    }

    public async Task<StudentManagementResult> SetAccountLockAsync(
        int studentId,
        bool locked,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.StudentProfiles.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == studentId, cancellationToken);
        if (profile?.User is null) return new(false, "Sinh viên chưa có tài khoản để thay đổi trạng thái.", studentId);

        await userManager.SetLockoutEnabledAsync(profile.User, true);
        var result = await userManager.SetLockoutEndDateAsync(
            profile.User,
            locked ? DateTimeOffset.UtcNow.AddYears(100) : null);
        return result.Succeeded
            ? new(true, locked ? $"Đã khóa tài khoản {profile.User.Email}." : $"Đã mở khóa tài khoản {profile.User.Email}.", profile.Id)
            : new(false, "Không thể cập nhật trạng thái tài khoản.", profile.Id);
    }

    public async Task<StudentManagementResult> ResetPasswordAsync(
        ResetStudentPasswordInput input,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.StudentProfiles.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == input.StudentId, cancellationToken);
        if (profile?.User is null) return new(false, "Sinh viên chưa có tài khoản để đặt lại mật khẩu.", input.StudentId);

        var token = await userManager.GeneratePasswordResetTokenAsync(profile.User);
        var result = await userManager.ResetPasswordAsync(profile.User, token, input.TemporaryPassword);
        return result.Succeeded
            ? new(true, $"Đã đặt lại mật khẩu cho {profile.User.Email}.", profile.Id)
            : new(false, string.Join(" ", result.Errors.Select(x => x.Description)), profile.Id);
    }

    private async Task<StudentDirectoryDetailViewModel> ToDetailAsync(
        StudentProfile student,
        CancellationToken cancellationToken)
    {
        var currentStay = student.Stays.SingleOrDefault();
        var requests = await db.HousingRequests.AsNoTracking().CountAsync(x => x.StudentProfileId == student.Id, cancellationToken);
        var balance = await billingLedger.GetOutstandingBalanceAsync(student.Id, cancellationToken);
        var history = await db.Stays.AsNoTracking()
            .Where(x => x.StudentProfileId == student.Id)
            .OrderByDescending(x => x.StartedAt)
            .Select(x => new StudentStayHistoryViewModel(
                x.Room.Code,
                x.Room.Floor.Building.Name,
                x.StartedAt,
                x.EndedAt,
                x.EndedAt == null))
            .ToListAsync(cancellationToken);
        return new StudentDirectoryDetailViewModel
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            Initials = Initials(student.FullName),
            Faculty = string.IsNullOrWhiteSpace(student.Faculty) ? "Chưa cập nhật" : student.Faculty,
            Gender = student.Gender,
            PhoneNumber = string.IsNullOrWhiteSpace(student.PhoneNumber) ? "Chưa cập nhật" : student.PhoneNumber,
            RoomCode = currentStay?.Room.Code,
            StayStartedAt = currentStay?.StartedAt,
            Email = student.User?.Email,
            HasAccount = student.UserId is not null,
            IsLocked = student.User?.LockoutEnd > DateTimeOffset.UtcNow,
            RequestCount = requests,
            OutstandingBalance = balance,
            StayHistory = history
        };
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static string Initials(string name) => string.Concat(name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .TakeLast(2)
        .Select(x => x[0]));
}
