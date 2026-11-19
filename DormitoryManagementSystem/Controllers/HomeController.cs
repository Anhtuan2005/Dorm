using System.Diagnostics;
using System.Security.Claims;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Controllers;

[Authorize]
public class HomeController(
    ILogger<HomeController> logger,
    IOperationsReportingService reporting) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (User.IsInRole(RoleNames.Student))
            return RedirectToAction("Dashboard", "Student");
        if (!User.IsInRole(RoleNames.Admin) && !User.IsInRole(RoleNames.DormManager))
            return Forbid();

        ViewData["Title"] = "Tổng quan";
        ViewData["ActiveNav"] = "dashboard";
        return View(await reporting.GetDashboardAsync(cancellationToken));
    }

    [HttpGet, Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> AttentionSnapshot(
        [FromServices] OperationsAttentionService attention, [FromServices] DormitoryDbContext db,
        CancellationToken cancellationToken)
    {
        var counts = await attention.GetAsync(cancellationToken);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var mine = db.Notifications.Where(x => x.UserId == userId);
        var unread = await mine.CountAsync(x => x.ReadAt == null, cancellationToken);
        var latest = await mine.MaxAsync(x => (long?)x.Id, cancellationToken) ?? 0;
        var applications = await db.HousingRequestEvents.MaxAsync(x => (long?)x.Id, cancellationToken) ?? 0;
        var maintenance = await db.MaintenanceUpdates.MaxAsync(x => (long?)x.Id, cancellationToken) ?? 0;
        return Json(new
        {
            counts.Applications, counts.Maintenance, counts.OverdueInvoices, unread,
            applicationsRevision = $"{applications}:{counts.Applications}",
            maintenanceRevision = $"{maintenance}:{counts.Maintenance}",
            notificationsRevision = $"{latest}:{unread}"
        });
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous, ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        logger.LogError("Yêu cầu lỗi với trace {TraceIdentifier}.", HttpContext.TraceIdentifier);
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
