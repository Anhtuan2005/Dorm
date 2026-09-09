using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class StudentManagementService(
    DormitoryDbContext db,
    UserManager<ApplicationUser> userManager) : IStudentManagementService
{
    public async Task<StudentDirectoryViewModel> GetDirectoryAsync(
        StudentDirectoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = db.StudentProfiles.AsNoTracking();
        var linkedCount = await baseQuery.CountAsync(x => x.UserId != null, cancellationToken);
        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var buildings = await db.Buildings.AsNoTracking().OrderBy(x => x.SortOrder).Select(x => x.Code).ToListAsync(cancellationToken);

        var students = await baseQuery
            .Include(x => x.User)
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
                .ThenInclude(x => x.Room).ThenInclude(x => x.Floor).ThenInclude(x => x.Building)
            .AsSplitQuery()
            .OrderBy(x => x.StudentCode)
            .ToListAsync(cancellationToken);

        IEnumerable<StudentProfile> filtered = students;
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(x =>
                x.StudentCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (x.User?.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (!string.IsNullOrWhiteSpace(query.Building))
            filtered = filtered.Where(x => x.Stays.SingleOrDefault()?.Room.Floor.Building.Code == query.Building);
        filtered = query.Account switch
        {
            "linked" => filtered.Where(x => x.UserId != null),
            "unlinked" => filtered.Where(x => x.UserId == null),
            "locked" => filtered.Where(x => x.User?.LockoutEnd > DateTimeOffset.UtcNow),
            _ => filtered
        };

        var listed = filtered.Take(120).ToList();
        var selected = listed.FirstOrDefault(x => x.Id == query.SelectedId) ?? listed.FirstOrDefault();
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
            UnlinkedAccountCount = totalCount - linkedCount
        };
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
        var balance = await db.Invoices.AsNoTracking()
            .Where(x => x.StudentProfileId == student.Id && x.Status != InvoiceStatus.Cancelled)
            .SumAsync(x => x.TotalAmount - x.PaidAmount, cancellationToken);
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
            OutstandingBalance = Math.Max(0, balance)
        };
    }

    private static string Initials(string name) => string.Concat(name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .TakeLast(2)
        .Select(x => x[0]));
}
