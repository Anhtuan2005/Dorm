using System.Diagnostics;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
