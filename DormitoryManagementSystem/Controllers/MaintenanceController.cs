using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
public sealed class MaintenanceController(
    IMaintenanceManagementService maintenance,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] MaintenanceQueueQuery query,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Sự cố";
        ViewData["ActiveNav"] = "maintenance";
        return View(await maintenance.GetQueueAsync(query, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        UpdateMaintenanceInput input,
        [Bind(Prefix = "Return")] MaintenanceReturnContext context,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(x => x.Errors).FirstOrDefault()?.ErrorMessage
                ?? "Dữ liệu cập nhật chưa hợp lệ.";
            return RedirectToQueue(input.RequestId, context);
        }

        var result = await maintenance.UpdateAsync(input, ManagerId, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToQueue(result.RequestId == 0 ? input.RequestId : result.RequestId, context);
    }

    private string ManagerId => userManager.GetUserId(User)
        ?? throw new InvalidOperationException("Không xác định được tài khoản quản lý.");

    private IActionResult RedirectToQueue(int requestId, MaintenanceReturnContext context) =>
        RedirectToAction(nameof(Index), new
        {
            context.Search,
            context.Status,
            context.Priority,
            context.Building,
            Page = Math.Max(1, context.Page),
            SelectedId = requestId > 0 ? (int?)requestId : null
        });
}
