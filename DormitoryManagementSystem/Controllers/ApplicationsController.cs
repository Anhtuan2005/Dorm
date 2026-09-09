using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
public sealed class ApplicationsController(
    IApplicationManagementService applicationService,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] ApplicationQueueQuery query,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Đơn đăng ký";
        ViewData["ActiveNav"] = "applications";
        return View(await applicationService.GetQueueAsync(query, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BeginReview(ApplicationActionInput input, CancellationToken cancellationToken) =>
        RedirectWith(await applicationService.BeginReviewAsync(input, ManagerId, cancellationToken), "attention");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestInformation(ApplicationActionInput input, CancellationToken cancellationToken) =>
        RedirectWith(await applicationService.RequestInformationAsync(input, ManagerId, cancellationToken), "attention");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(ApplicationActionInput input, CancellationToken cancellationToken) =>
        RedirectWith(await applicationService.RejectAsync(input, ManagerId, cancellationToken), "completed");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ApplicationActionInput input, CancellationToken cancellationToken) =>
        RedirectWith(await applicationService.ApproveAndAssignAsync(input, ManagerId, cancellationToken), "completed");

    private string ManagerId => userManager.GetUserId(User)
        ?? throw new InvalidOperationException("Không xác định được tài khoản quản lý.");

    private IActionResult RedirectWith(ApplicationManagementResult result, string successScope)
    {
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index), new
        {
            Scope = result.Success ? successScope : "attention",
            SelectedId = result.RequestId
        });
    }
}
