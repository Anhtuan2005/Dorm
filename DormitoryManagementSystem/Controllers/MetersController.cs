using System.Globalization;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = RoleNames.Admin + "," + RoleNames.DormManager), AutoValidateAntiforgeryToken]
public sealed class MetersController(DormitoryDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? periodId, int? roomId, CancellationToken token)
    {
        ViewData["Title"] = "Chỉ số điện nước";
        ViewData["ActiveNav"] = "billing";
        var periods = await db.BillingPeriods.AsNoTracking().Where(x => x.Metered).OrderByDescending(x => x.StartDate).ToListAsync(token);
        var period = periods.FirstOrDefault(x => x.Id == periodId) ?? periods.FirstOrDefault();
        var readings = period == null ? [] : await db.RoomMeterReadings.AsNoTracking().Where(x => x.BillingPeriodId == period.Id)
            .OrderBy(x => x.Room.Code).Select(x => new MeterRow(x.RoomId, x.Room.Code, x.PowerStart, x.PowerEnd, x.PowerRate,
                x.WaterStart, x.WaterEnd, x.WaterRate, x.RowVersion)).ToListAsync(token);
        return View(new MeterPage(periods, period, period != null && await db.Invoices.AnyAsync(x => x.BillingPeriodId == period.Id, token),
            await db.Rooms.OrderBy(x => x.Code).Select(x => x.Code).ToListAsync(token), readings, readings.FirstOrDefault(x => x.RoomId == roomId)));
    }

    [HttpPost]
    public async Task<IActionResult> CreatePeriod(IssueInvoicesInput input, CancellationToken token)
    {
        int id = 0;
        var error = await WorkTransaction.RunAsync(db, async () => {
            if (!ModelState.IsValid || input.StartDate == null || input.EndDate == null || input.DueDate == null ||
                input.EndDate < input.StartDate || input.DueDate < input.StartDate || input.EndDate.Value.Year > 2100)
                return "Kiểm tra tên, mã và ngày của kỳ thu.";
            var code = input.PeriodCode.Trim().ToUpperInvariant();
            var row = await db.BillingPeriods.SingleOrDefaultAsync(x => x.Code == code, token);
            if (row != null && (row.StartDate != input.StartDate || row.EndDate != input.EndDate || row.DueDate != input.DueDate ||
                await db.Invoices.AnyAsync(x => x.BillingPeriodId == row.Id, token))) return "Kỳ thu đã phát hành hoặc có mốc ngày khác.";
            if (row == null)
            {
                row = new() { Code = code, Name = input.PeriodName.Trim(), StartDate = input.StartDate.Value,
                    EndDate = input.EndDate.Value, DueDate = input.DueDate.Value };
                db.BillingPeriods.Add(row);
            }
            row.Metered = true;
            await db.SaveChangesAsync(token);
            id = row.Id;
            return null;
        }, token);
        return Back(error, "Đã mở sổ chỉ số điện nước.", id);
    }

    [HttpPost]
    public async Task<IActionResult> Reading(int periodId, string roomCode, string? version,
        string powerStart, string powerEnd, string powerRate, string waterStart, string waterEnd, string waterRate, CancellationToken token)
    {
        var raw = new[] { powerStart, powerEnd, powerRate, waterStart, waterEnd, waterRate };
        var numbers = new decimal[6];
        for (int i = 0; i < raw.Length; i++)
            if (!decimal.TryParse(raw[i], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out numbers[i]) ||
                numbers[i] is < 0 or > 99999999 || decimal.Round(numbers[i], 2) != numbers[i])
                return Back("Chỉ số và đơn giá phải là số không âm, tối đa hai chữ số thập phân.", "", periodId);
        if (numbers[1] < numbers[0] || numbers[4] < numbers[3] ||
            (numbers[1] - numbers[0]) * numbers[2] > 999999999m || (numbers[4] - numbers[3]) * numbers[5] > 999999999m)
            return Back("Kiểm tra chỉ số cuối và thành tiền mỗi khoản (tối đa 999.999.999 đ).", "", periodId);
        var error = await WorkTransaction.RunAsync(db, async () => {
            if (!await db.BillingPeriods.AnyAsync(x => x.Id == periodId && x.Metered, token) ||
                await db.Invoices.AnyAsync(x => x.BillingPeriodId == periodId, token)) return "Chỉ được sửa trước khi phát hành kỳ chỉ số.";
            var room = await db.Rooms.SingleOrDefaultAsync(x => x.Code == roomCode, token);
            if (room == null) return "Không tìm thấy mã phòng.";
            var row = await db.RoomMeterReadings.FindAsync([periodId, room.Id], token);
            if (row == null && !string.IsNullOrEmpty(version) || row != null && Convert.ToBase64String(row.RowVersion) != version)
                return "Mở dòng chỉ số hiện tại trước khi sửa.";
            if (row == null) { row = new() { RoomId = room.Id, BillingPeriodId = periodId }; db.RoomMeterReadings.Add(row); }
            row.PowerStart = numbers[0]; row.PowerEnd = numbers[1]; row.PowerRate = numbers[2];
            row.WaterStart = numbers[3]; row.WaterEnd = numbers[4]; row.WaterRate = numbers[5];
            return null;
        }, token);
        return Back(error, "Đã lưu chỉ số phòng.", periodId);
    }

    private IActionResult Back(string? error, string ok, int periodId)
    {
        TempData[error == null ? "Success" : "Error"] = error ?? ok;
        return RedirectToAction(nameof(Index), new { periodId });
    }
}
