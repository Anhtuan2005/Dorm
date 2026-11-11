using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
public sealed class SettingsController(IOperationsReportingService reporting) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Cài đặt";
        ViewData["ActiveNav"] = "settings";
        return View(await reporting.GetSettingsAsync(cancellationToken));
    }
}
