using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Controllers;

[Authorize]
[Route("attachments")]
public sealed class AttachmentsController(
    IMaintenanceAttachmentStorage storage,
    UserManager<ApplicationUser> userManager,
    DormitoryDbContext db) : Controller
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

    [HttpGet("rooms/{id:long}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager},{RoleNames.Student}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> RoomPhoto(long id, CancellationToken cancellationToken)
    {
        var photo = await db.RoomPhotos.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Data, x.ContentType }).SingleOrDefaultAsync(cancellationToken);
        if (photo is null) return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(photo.Data, photo.ContentType);
    }
}
