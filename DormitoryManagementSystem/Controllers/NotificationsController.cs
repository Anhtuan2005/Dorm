using System.Security.Claims;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotificationsController(DormitoryDbContext db, TimeProvider clock, RealtimeUpdates live) : Controller
{
    private const int PageSize = 25;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<Notification> Mine => db.Notifications.Where(x => x.UserId == UserId);

    [HttpGet]
    public async Task<IActionResult> Index(bool unreadOnly = false, int page = 1, CancellationToken cancellationToken = default)
    {
        var unread = await Mine.CountAsync(x => x.ReadAt == null, cancellationToken);
        var filtered = unreadOnly ? Mine.Where(x => x.ReadAt == null) : Mine;
        var count = unreadOnly ? unread : await filtered.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)count / PageSize));
        page = Math.Clamp(page, 1, totalPages);
        var items = await filtered.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);
        var model = new OperationsNotificationsViewModel(items, unreadOnly, unread, page, totalPages);

        ViewData["Title"] = "Thông báo";
        ViewData["ActiveNav"] = "notifications";
        return Request.Headers["X-Operations-Refresh"] == "true" ? PartialView(nameof(Index), model) : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(long id, CancellationToken cancellationToken)
    {
        var notification = await Mine.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notification is null) return NotFound();
        await MarkReadAsync(Mine.Where(x => x.Id == id), cancellationToken);
        return Url.IsLocalUrl(notification.TargetUrl) ? LocalRedirect(notification.TargetUrl) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await MarkReadAsync(Mine, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task MarkReadAsync(IQueryable<Notification> source, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var count = await source.Where(x => x.ReadAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ReadAt, now), cancellationToken);
        if (count != 0) await live.NotifyOperationsInboxAsync(UserId);
    }
}
