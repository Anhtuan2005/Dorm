using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.ViewComponents;

public sealed class StudentNotificationBadgeViewComponent(
    IStudentPortalService portal,
    UserManager<ApplicationUser> userManager) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = userManager.GetUserId(UserClaimsPrincipal);
        if (userId is null) return Content("0");
        var count = await portal.GetUnreadNotificationCountAsync(userId, HttpContext.RequestAborted);
        return Content(count > 99 ? "99+" : count.ToString());
    }
}
