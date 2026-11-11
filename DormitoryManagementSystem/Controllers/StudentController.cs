using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = RoleNames.Student)]
public sealed class StudentController(
    IStudentPortalService studentPortalService,
    UserManager<Models.ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        SetPage("Trang chủ", "dashboard");
        return View(await studentPortalService.GetDashboardAsync(UserId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Notifications(CancellationToken cancellationToken)
    {
        SetPage("Thông báo", "notifications");
        return View(await studentPortalService.GetNotificationsAsync(UserId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> OpenNotification(long id, CancellationToken cancellationToken)
    {
        var target = await studentPortalService.ReadNotificationAsync(UserId, id, cancellationToken);
        return target is null ? RedirectToAction(nameof(Notifications)) : LocalRedirect(target);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken cancellationToken)
    {
        var count = await studentPortalService.MarkAllNotificationsReadAsync(UserId, cancellationToken);
        TempData["Success"] = count == 0 ? "Bạn không có thông báo chưa đọc." : $"Đã đánh dấu {count} thông báo là đã đọc.";
        return RedirectToAction(nameof(Notifications));
    }

    [HttpGet]
    public async Task<IActionResult> Rooms(
        [FromQuery] StudentRoomSearchQuery query,
        CancellationToken cancellationToken)
    {
        SetPage("Tìm phòng", "rooms");
        return View(await studentPortalService.SearchRoomsAsync(UserId, query, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Applications(CancellationToken cancellationToken)
    {
        SetPage("Đăng ký của tôi", "applications");
        return View(await studentPortalService.GetApplicationsAsync(UserId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitHousingRequest(
        SubmitHousingRequestInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(Rooms));
        }

        var result = await studentPortalService.SubmitHousingRequestAsync(UserId, input, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return result.Success ? RedirectToAction(nameof(Applications)) : RedirectToAction(nameof(Rooms));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelHousingRequest(
        StudentHousingRequestActionInput input,
        CancellationToken cancellationToken)
    {
        var result = await studentPortalService.CancelHousingRequestAsync(UserId, input, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Applications));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SupplementHousingRequest(
        SupplementHousingRequestInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = FirstValidationError("Nội dung bổ sung chưa hợp lệ.");
            return RedirectToAction(nameof(Applications));
        }

        var result = await studentPortalService.SupplementHousingRequestAsync(UserId, input, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Applications));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitCheckoutRequest(
        SubmitCheckoutRequestInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = FirstValidationError("Lý do trả phòng chưa hợp lệ.");
            return RedirectToAction(nameof(Applications));
        }

        var result = await studentPortalService.SubmitCheckoutRequestAsync(UserId, input, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Applications));
    }

    [HttpGet]
    public async Task<IActionResult> Invoices(CancellationToken cancellationToken)
    {
        SetPage("Hóa đơn", "invoices");
        return View(await studentPortalService.GetInvoicesAsync(UserId, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Repairs(CancellationToken cancellationToken)
    {
        SetPage("Yêu cầu hỗ trợ", "repairs");
        return View(await studentPortalService.GetRepairsAsync(UserId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRepair(
        CreateRepairRequestInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(Repairs));
        }

        var result = await studentPortalService.CreateRepairAsync(UserId, input, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Repairs));
    }

    private string UserId => userManager.GetUserId(User)
        ?? throw new InvalidOperationException("Không xác định được tài khoản đăng nhập.");

    private string FirstValidationError(string fallback) => ModelState.Values
        .SelectMany(x => x.Errors)
        .Select(x => x.ErrorMessage)
        .FirstOrDefault() ?? fallback;

    private void SetPage(string title, string activeNav)
    {
        ViewData["Title"] = title;
        ViewData["ActiveStudentNav"] = activeNav;
    }
}
