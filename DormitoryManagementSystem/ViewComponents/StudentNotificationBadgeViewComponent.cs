using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.ViewComponents;

public sealed class StudentNotificationBadgeViewComponent(
    DormitoryDbContext db,
    UserManager<ApplicationUser> userManager) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = userManager.GetUserId(UserClaimsPrincipal);
        if (userId is null) return Content("0");
        var count = await db.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && x.ReadAt == null);
        return Content(count > 99 ? "99+" : count.ToString());
    }
}
