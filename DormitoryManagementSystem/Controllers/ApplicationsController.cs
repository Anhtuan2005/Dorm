using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        ViewData["Title"] = "Đơn lưu trú";
        ViewData["ActiveNav"] = "applications";
        var model = await applicationService.GetQueueAsync(query, cancellationToken);
        return Request.Headers["X-Operations-Refresh"] == "true" ? PartialView(nameof(Index), model) : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BeginReview(ApplicationActionInput input, CancellationToken cancellationToken) =>
        RedirectWith(await applicationService.ExecuteAsync(input, HousingRequestAction.BeginReview, ManagerId, cancellationToken), "attention");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestInformation(ApplicationActionInput input, CancellationToken cancellationToken) =>
        RedirectWith(await applicationService.ExecuteAsync(input, HousingRequestAction.RequestInformation, ManagerId, cancellationToken), "attention");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(ApplicationActionInput input, CancellationToken cancellationToken) =>
        RedirectWith(await applicationService.ExecuteAsync(input, HousingRequestAction.Reject, ManagerId, cancellationToken), "completed");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ApplicationActionInput input, CancellationToken cancellationToken) =>
        RedirectWith(await applicationService.ExecuteAsync(input, HousingRequestAction.Approve, ManagerId, cancellationToken), "attention");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmMoveIn(ApplicationActionInput input, CancellationToken cancellationToken) =>
        RedirectWith(await applicationService.ExecuteAsync(input, HousingRequestAction.ConfirmMoveIn, ManagerId, cancellationToken), "completed");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(long id, string rowVersion, DateTime? appointment,
        [FromServices] Data.DormitoryDbContext db, [FromServices] TimeProvider clock,
        [FromServices] RealtimeUpdates live, CancellationToken token)
    {
        string? student = null;
        var error = await WorkTransaction.RunAsync(db, async () =>
        {
            var request = await db.HousingRequests.Include(x => x.StudentProfile).Include(x => x.ApprovedRoomHold)
                .SingleOrDefaultAsync(x => x.Id == id, token);
            if (request == null || Convert.ToBase64String(request.RowVersion) != rowVersion)
                return "Đơn đã thay đổi. Hãy mở lại để đặt lịch.";
            if (!(request.Type == HousingRequestType.Checkout && request.Status == HousingRequestStatus.UnderReview) &&
                request.Status != HousingRequestStatus.AwaitingMoveIn) return "Đơn chưa ở bước bàn giao.";
            var now = clock.GetUtcNow().UtcDateTime;
            if (appointment == null || appointment <= now.AddHours(7) || appointment.Value.Year > 2100)
                return "Chọn thời gian hẹn trong tương lai theo giờ Việt Nam.";
            var utc = DateTime.SpecifyKind(appointment.Value.AddHours(-7), DateTimeKind.Utc);
            if (request.ApprovedRoomHold?.ExpiresAt <= utc) return "Lịch hẹn phải trước hạn giữ giường.";
            request.AppointmentUtc = utc;
            var note = $"Hẹn bàn giao lúc {appointment:HH:mm dd/MM/yyyy} (giờ Việt Nam).";
            request.Events.Add(new() { Status = request.Status, ActorUserId = ManagerId, Note = note, CreatedAt = now });
            student = request.StudentProfile.UserId;
            if (student != null) db.Notifications.Add(new() { UserId = student, Type = "Application", Title = "Lịch bàn giao phòng",
                Body = request.Code + " · " + note, TargetUrl = "/Student/Applications", CreatedAt = now });
            return null;
        }, token);
        if (error == null) await live.NotifyOperationsAsync(student);
        return RedirectWith(new(error == null, error ?? "Đã lưu lịch bàn giao.", id), "attention");
    }

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
