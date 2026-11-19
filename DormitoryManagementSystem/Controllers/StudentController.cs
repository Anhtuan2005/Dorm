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
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        SetPage("Hồ sơ của tôi", "profile");
        return View(await studentPortalService.GetMyProfileAsync(UserId, cancellationToken));
    }

    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        SetPage("Trang chủ", "dashboard");
        return View(await studentPortalService.GetDashboardAsync(UserId, cancellationToken));
    }

    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Notifications(int page = 1, CancellationToken cancellationToken = default)
    {
        SetPage("Thông báo", "notifications");
        return View(await studentPortalService.GetNotificationsAsync(UserId, page, cancellationToken));
    }

    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> NotificationSnapshot([FromQuery] string? panel, int page = 1, CancellationToken cancellationToken = default)
    {
        if (panel == "inbox")
        {
            var inbox = await studentPortalService.GetNotificationsAsync(UserId, page, cancellationToken);
            Response.Headers["X-Notifications-Unread"] = inbox.UnreadCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return PartialView("_NotificationInbox", inbox);
        }

        var unread = await studentPortalService.GetUnreadNotificationCountAsync(UserId, cancellationToken);
        if (panel == "recent")
        {
            var recent = await studentPortalService.GetRecentNotificationsAsync(UserId, cancellationToken);
            Response.Headers["X-Notifications-Unread"] = unread.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return PartialView("_RecentNotifications", recent);
        }

        return Json(new { unread });
    }

    [HttpGet]
    public async Task<IActionResult> OpenNotification(long id, CancellationToken cancellationToken)
    {
        var target = await studentPortalService.ReadNotificationAsync(UserId, id, cancellationToken);
        return target is null ? RedirectToAction(nameof(Notifications)) : LocalRedirect(target);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllNotificationsRead(int page = 1, CancellationToken cancellationToken = default)
    {
        var count = await studentPortalService.MarkAllNotificationsReadAsync(UserId, cancellationToken);
        TempData["Success"] = count == 0 ? "Bạn không có thông báo chưa đọc." : $"Đã đánh dấu {count} thông báo là đã đọc.";
        return RedirectToAction(nameof(Notifications), page > 1 ? new { page } : null);
    }

    [HttpGet]
    public async Task<IActionResult> Rooms(
        [FromQuery] StudentRoomSearchQuery query,
        CancellationToken cancellationToken)
    {
        SetPage("Tìm phòng", "rooms");
        return View(await studentPortalService.SearchRoomsAsync(UserId, query, cancellationToken));
    }

    [HttpGet("Student/RoomPreview/{id:int}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> RoomPreview(int id, CancellationToken cancellationToken)
    {
        var preview = await studentPortalService.GetRoomPreviewAsync(UserId, id, cancellationToken);
        if (preview is null) return NotFound();
        SetPage($"Phòng {preview.Room.Code}", "rooms");
        return View(preview);
    }

    [HttpGet]
    public async Task<IActionResult> Applications(CancellationToken cancellationToken)
    {
        SetPage("Đăng ký của tôi", "applications");
        return View(await studentPortalService.GetApplicationsAsync(UserId, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SubmitHousingRequest(
        SubmitHousingRequestInput input,
        CancellationToken cancellationToken)
    {
        var result = ModelState.IsValid
            ? await studentPortalService.SubmitHousingRequestAsync(UserId, input, cancellationToken)
            : new StudentOperationResult(false, FirstValidationError("Vui lòng kiểm tra thông tin đăng ký."));
        if (result.Success)
        {
            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Applications));
        }

        var preview = await studentPortalService.GetRoomPreviewAsync(UserId, input.TargetRoomId, cancellationToken);
        TempData["Error"] = result.Message;
        if (preview is null) return RedirectToAction(nameof(Rooms));
        SetPage($"Phòng {preview.Room.Code}", "rooms");
        return View(nameof(RoomPreview), preview with { Draft = input });
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
    public async Task<IActionResult> Invoices([FromServices] VnpayCheckoutService vnpay, CancellationToken cancellationToken)
    {
        SetPage("Hóa đơn", "invoices");
        ViewData["VnpayEnabled"] = vnpay.CanStart;
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
    public async Task<IActionResult> FollowRepair(int requestId, bool following, CancellationToken cancellationToken)
    {
        var result = await studentPortalService.SetRepairFollowingAsync(UserId, requestId, following, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return result.Success
            ? RedirectToAction(nameof(Repairs), null, null, $"repair-{requestId}")
            : RedirectToAction(nameof(Repairs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRepair(
        CreateRepairRequestInput input,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await studentPortalService.CreateRepairAsync(UserId, input, cancellationToken);
            if (result.Success)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(Repairs));
            }
            ModelState.AddModelError(string.Empty, result.Message);
        }

        var page = await studentPortalService.GetRepairsAsync(UserId, cancellationToken);
        SetPage("Yêu cầu hỗ trợ", "repairs");
        ViewData["OpenRepairDialog"] = page.CanCreateRepair;
        ViewData["RepairAttachmentsCleared"] = input.Attachments.Count > 0;
        if (!page.CanCreateRepair) TempData["Error"] = FirstValidationError("Bạn chưa có phòng hiện tại để gửi yêu cầu.");
        return View(nameof(Repairs), new StudentRepairsViewModel
        {
            Student = page.Student,
            CurrentRoomCode = page.CurrentRoomCode,
            CanCreateRepair = page.CanCreateRepair,
            Repairs = page.Repairs,
            SharedRepairs = page.SharedRepairs,
            NewRequest = new CreateRepairRequestInput
            {
                Category = input.Category,
                Priority = input.Priority,
                Title = input.Title,
                Description = input.Description,
                ShareWithRoom = input.ShareWithRoom
            }
        });
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
