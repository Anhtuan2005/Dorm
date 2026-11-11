using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

[Authorize]
[Route("attachments")]
public sealed class AttachmentsController(
    IMaintenanceAttachmentStorage storage,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("maintenance/{id:long}")]
    public async Task<IActionResult> Maintenance(long id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();
        var canViewAll = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.DormManager);
        var file = await storage.OpenAsync(id, userId, canViewAll, cancellationToken);
        return file is null
            ? NotFound()
            : File(file.Stream, file.ContentType, enableRangeProcessing: true);
    }
}
