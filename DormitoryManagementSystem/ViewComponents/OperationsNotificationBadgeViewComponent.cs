using System.Security.Claims;
using DormitoryManagementSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.ViewComponents;

public sealed class OperationsNotificationBadgeViewComponent(DormitoryDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = await db.Notifications.CountAsync(x => x.UserId == userId && x.ReadAt == null, HttpContext.RequestAborted);
        return Content(unread > 99 ? "99+" : unread.ToString());
    }
}
