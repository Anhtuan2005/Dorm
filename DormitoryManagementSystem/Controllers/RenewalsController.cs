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

[Authorize, AutoValidateAntiforgeryToken]
public sealed class RenewalsController(DormitoryDbContext db, TimeProvider clock, RealtimeUpdates live) : Controller
{
    private const string Staff = RoleNames.Admin + "," + RoleNames.DormManager;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));
    private static IQueryable<RenewalRow> Rows(IQueryable<StayRenewal> rows) => rows.Select(x =>
        new RenewalRow(x.Id, x.Stay.StudentProfile.FullName, x.Stay.Room.Code, x.ResidenceTerm.Name, x.ResidenceTerm.Ends, x.Approved, x.Note));

    [HttpGet, Authorize(Roles = Staff)]
    public async Task<IActionResult> Index(int page = 1, CancellationToken token = default)
    {
        ViewData["Title"] = "Gia hạn nội trú";
        ViewData["ActiveNav"] = "applications";
        page = Math.Clamp(page, 1, 100000);
        var requests = await Rows(db.StayRenewals.OrderBy(x => x.DecidedUtc != null).ThenByDescending(x => x.RequestedUtc)
            .ThenByDescending(x => x.Id).Skip((page - 1) * 25).Take(26)).ToListAsync(token);
        var threshold = Today.AddDays(30);
        return View(new RenewalPage {
            Requests = requests.Take(25).ToList(), Page = page, More = requests.Count > 25,
            Terms = await db.ResidenceTerms.AsNoTracking().Where(x => x.Ends >= Today).OrderBy(x => x.Starts).ToListAsync(token),
            Expiring = await db.Stays.Where(x => x.EndedAt == null && x.PlannedEnd <= threshold)
                .OrderBy(x => x.PlannedEnd).ThenBy(x => x.Id)
                .Select(x => new ExpiringStay(x.StudentProfile.StudentCode + " · " + x.StudentProfile.FullName, x.Room.Code, x.PlannedEnd)).ToListAsync(token)
        });
    }

    [HttpGet, Authorize(Roles = RoleNames.Student)]
    public async Task<IActionResult> Mine(CancellationToken token)
    {
        ViewData["Title"] = "Gia hạn nội trú";
        ViewData["ActiveStudentNav"] = "applications";
        var stay = await db.Stays.AsNoTracking().SingleOrDefaultAsync(x => x.StudentProfile.UserId == UserId && x.EndedAt == null, token);
        var history = db.StayRenewals.Where(x => x.Stay.StudentProfile.UserId == UserId);
        var terms = db.ResidenceTerms.AsNoTracking().Where(x => x.Opens <= Today && x.Closes >= Today && x.Ends >= Today);
        if (stay != null)
            terms = terms.Where(x => (stay.PlannedEnd == null || x.Ends > stay.PlannedEnd) &&
                !db.StayRenewals.Any(r => r.StayId == stay.Id && (r.ResidenceTermId == x.Id || r.Approved == null)));
        return View(new RenewalPage { StayId = stay?.Id, CurrentEnd = stay?.PlannedEnd,
            Terms = stay == null ? [] : await terms.OrderBy(x => x.Starts).ToListAsync(token),
            Requests = await Rows(history.OrderByDescending(x => x.RequestedUtc)).ToListAsync(token) });
    }

    [HttpPost, Authorize(Roles = Staff)]
    public async Task<IActionResult> Term(string name, DateOnly? opens, DateOnly? closes, DateOnly? starts, DateOnly? ends, CancellationToken token)
    {
        var error = await WorkTransaction.RunAsync(db, () =>
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80 || opens == null || closes == null || starts == null ||
                ends == null || opens > closes || starts > ends || closes > ends || ends < Today)
                return Task.FromResult<string?>("Kiểm tra tên học kỳ, thời gian nhận đơn và ngày kết thúc.");
            db.ResidenceTerms.Add(new() { Name = name.Trim(), Opens = opens.Value, Closes = closes.Value, Starts = starts.Value, Ends = ends.Value });
            return Task.FromResult<string?>(null);
        }, token);
        return Done(error, "Đã mở đợt gia hạn.");
    }

    [HttpPost, Authorize(Roles = Staff)]
    public async Task<IActionResult> InitialEnd(string studentCode, DateOnly? ends, CancellationToken token)
    {
        var error = await WorkTransaction.RunAsync(db, async () => {
            var stay = await db.Stays.SingleOrDefaultAsync(x => x.StudentProfile.StudentCode == studentCode && x.EndedAt == null, token);
            if (stay == null || stay.PlannedEnd != null || ends == null || ends < DateOnly.FromDateTime(stay.StartedAt.AddHours(7)))
                return "Kiểm tra mã sinh viên và hạn ở. Chỉ khởi tạo hạn cho lượt đang ở chưa có ngày hết hạn.";
            stay.PlannedEnd = ends;
            return null;
        }, token);
        return Done(error, "Đã cập nhật hạn ở hiện tại.");
    }

    [HttpPost, Authorize(Roles = RoleNames.Student)]
    public async Task<IActionResult> Apply(int stayId, int termId, CancellationToken token)
    {
        var error = await WorkTransaction.RunAsync(db, async () => {
            var stay = await db.Stays.Include(x => x.StudentProfile).SingleOrDefaultAsync(x => x.Id == stayId &&
                x.StudentProfile.UserId == UserId && x.EndedAt == null, token);
            var term = await db.ResidenceTerms.FindAsync([termId], token);
            if (stay == null || term == null || term.Opens > Today || term.Closes < Today || term.Ends <= stay.PlannedEnd || term.Ends < Today)
                return "Lượt ở hoặc đợt gia hạn không còn phù hợp.";
            if (await db.StayRenewals.AnyAsync(x => x.StayId == stayId && (x.Approved == null || x.ResidenceTermId == termId), token))
                return "Bạn đã gửi đơn gia hạn. Hãy xem lịch sử bên dưới.";
            var now = clock.GetUtcNow().UtcDateTime;
            db.StayRenewals.Add(new() { StayId = stayId, ResidenceTermId = termId, RequestedUtc = now });
            await OperationsNotifications.StageAsync(db, "Renewal", "Có đơn gia hạn nội trú", stay.StudentProfile.StudentCode + " · " + term.Name, "/Renewals", now, token);
            return null;
        }, token);
        if (error == null) await live.NotifyOperationsAsync();
        return Done(error, "Đã gửi đơn gia hạn.", nameof(Mine));
    }

    [HttpPost, Authorize(Roles = Staff)]
    public async Task<IActionResult> Decide(int id, bool? approve, string? note, CancellationToken token)
    {
        string? recipient = null;
        var error = await WorkTransaction.RunAsync(db, async () => {
            var row = await db.StayRenewals.Include(x => x.ResidenceTerm).Include(x => x.Stay.StudentProfile)
                .SingleOrDefaultAsync(x => x.Id == id, token);
            if (row == null || row.Approved != null || approve == null) return "Đơn không còn chờ quyết định.";
            if (note?.Length > 300 || (approve == false && string.IsNullOrWhiteSpace(note))) return "Nhập lý do khi từ chối, tối đa 300 ký tự.";
            if (approve.Value)
            {
                if (row.Stay.EndedAt != null || row.ResidenceTerm.Ends <= row.Stay.PlannedEnd || row.ResidenceTerm.Ends < Today)
                    return "Lượt ở đã kết thúc hoặc thời hạn mới không còn phù hợp.";
                row.Stay.PlannedEnd = row.ResidenceTerm.Ends;
            }
            row.Approved = approve;
            row.DecidedById = UserId;
            row.DecidedUtc = clock.GetUtcNow().UtcDateTime;
            row.Note = note?.Trim();
            recipient = row.Stay.StudentProfile.UserId;
            if (recipient != null) db.Notifications.Add(new() { UserId = recipient, Title = approve.Value ? "Đã duyệt gia hạn" : "Đã từ chối gia hạn",
                Body = row.ResidenceTerm.Name, Type = "Renewal", TargetUrl = "/Renewals/Mine", CreatedAt = row.DecidedUtc.Value });
            return null;
        }, token);
        if (error == null) await live.NotifyOperationsAsync(recipient);
        return Done(error, "Đã lưu quyết định gia hạn.");
    }

    private IActionResult Done(string? error, string success, string action = nameof(Index))
    {
        TempData[error == null ? "Success" : "Error"] = error ?? success;
        return RedirectToAction(action);
    }
}
