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

    [HttpGet]
    public async Task<IActionResult> Peak([FromServices] Data.DormitoryDbContext db,
        [FromServices] TimeProvider clock, CancellationToken token)
    {
        ViewData["Title"] = "Đợt đăng ký cao điểm";
        ViewData["ActiveNav"] = "applications";
        ViewData["Today"] = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));
        return View(await db.PeakIntakes.AsNoTracking().OrderByDescending(x => x.Starts).ThenByDescending(x => x.Id).ToListAsync(token));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenPeak(DateOnly? starts, DateOnly? ends, int holdHours,
        [FromServices] Data.DormitoryDbContext db, [FromServices] TimeProvider clock, CancellationToken token)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));
        var error = await WorkTransaction.RunAsync(db, async () =>
        {
            if (starts == null || ends == null || starts < today || ends < starts || holdHours is < 1 or > 72)
                return "Chọn ngày bắt đầu từ hôm nay, ngày kết thúc hợp lệ và thời hạn giữ từ 1 đến 72 giờ.";
            if (await db.PeakIntakes.AnyAsync(x => x.ClosedUtc == null && x.Starts <= ends && x.Ends >= starts, token))
                return "Khoảng ngày trùng với một đợt đã lên lịch.";
            db.PeakIntakes.Add(new() { Starts = starts.Value, Ends = ends.Value, HoldHours = holdHours });
            return null;
        }, token);
        TempData[error == null ? "Success" : "Error"] = error ?? "Đã lưu lịch đăng ký cao điểm.";
        return RedirectToAction(nameof(Peak));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ClosePeak(int id, [FromServices] Data.DormitoryDbContext db,
        [FromServices] TimeProvider clock, CancellationToken token)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now.AddHours(7));
        var changed = await db.PeakIntakes.Where(x => x.Id == id && x.ClosedUtc == null && x.Ends >= today)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ClosedUtc, now), token);
        TempData[changed == 1 ? "Success" : "Error"] = changed == 1 ? "Đã kết thúc đợt. Các chỗ đã giữ vẫn có hiệu lực đến hạn riêng." : "Đợt đã kết thúc.";
        return RedirectToAction(nameof(Peak));
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
