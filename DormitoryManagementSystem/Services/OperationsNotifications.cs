using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

internal static class OperationsNotifications
{
    internal static Task StageApplicationAsync(DormitoryDbContext db, HousingRequest request, string title,
        DateTime now, CancellationToken cancellationToken) =>
        StageAsync(db, "Application", title, $"Đơn {request.Code}.",
            $"/Applications?Scope=all&Search={Uri.EscapeDataString(request.Code)}", now, cancellationToken);

    internal static Task StageMaintenanceAsync(DormitoryDbContext db, MaintenanceRequest request,
        DateTime now, CancellationToken cancellationToken) =>
        StageAsync(db, "Maintenance", "Sinh viên gửi sự cố mới", $"{request.Code} · {request.Title}",
            $"/Maintenance?Search={Uri.EscapeDataString(request.Code)}", now, cancellationToken);

    private static async Task StageAsync(DormitoryDbContext db, string type, string title, string body,
        string target, DateTime now, CancellationToken cancellationToken)
    {
        var recipients = await (from membership in db.UserRoles
            join role in db.Roles on membership.RoleId equals role.Id
            where role.Name == RoleNames.Admin || role.Name == RoleNames.DormManager
            select membership.UserId).Distinct().ToListAsync(cancellationToken);

        // The caller saves the student action and its notifications together.
        foreach (var userId in recipients)
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Body = body,
                TargetUrl = target,
                CreatedAt = now
            });
    }
}
