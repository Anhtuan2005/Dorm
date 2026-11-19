using System.Globalization;
using System.Text;
using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
public sealed class ReportsController(IOperationsReportingService reporting) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] OperationsReportQuery query,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Báo cáo";
        ViewData["ActiveNav"] = "reports";
        return View(await reporting.GetReportAsync(query, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] OperationsReportQuery query,
        CancellationToken cancellationToken)
    {
        var report = await reporting.GetReportAsync(query, cancellationToken);
        var hasPeriod = report.Query.PeriodId.HasValue;
        var csv = new StringBuilder("\uFEFF");
        csv.AppendLine("Báo cáo vận hành NẾP KTX");
        csv.AppendLine($"Kỳ;{Csv(report.PeriodName)}");
        csv.AppendLine($"Phạm vi;{Csv(report.Query.Building is null ? "Toàn KTX" : $"Tòa {report.Query.Building}")}");
        csv.AppendLine();
        csv.AppendLine("Tòa;Sức chứa;Đang ở;Đang giữ;Tạm khóa;Có thể xếp;Tỷ lệ lấp đầy;Công nợ;Sự cố mở");
        foreach (var building in report.Buildings)
        {
            csv.AppendLine(string.Join(';',
                Csv(building.Name),
                building.Capacity.Capacity,
                building.Capacity.Occupied,
                building.Capacity.Held,
                building.Capacity.Blocked,
                building.Capacity.Assignable,
                building.Capacity.OccupancyRate.ToString("0.0", CultureInfo.InvariantCulture),
                hasPeriod ? building.OutstandingBalance.ToString("0", CultureInfo.InvariantCulture) : "",
                building.ActiveIncidents));
        }
        if (hasPeriod)
        {
            csv.AppendLine();
            csv.AppendLine("Chỉ số tài chính;Giá trị");
            csv.AppendLine($"Đã phát hành;{report.Finance.Invoiced.ToString("0", CultureInfo.InvariantCulture)}");
            csv.AppendLine($"Đã thu ròng;{report.Finance.Collected.ToString("0", CultureInfo.InvariantCulture)}");
            csv.AppendLine($"Công nợ;{report.Finance.Outstanding.ToString("0", CultureInfo.InvariantCulture)}");
            csv.AppendLine($"Quá hạn;{report.Finance.Overdue.ToString("0", CultureInfo.InvariantCulture)}");
            csv.AppendLine();
            csv.AppendLine("Đơn lưu trú trong kỳ;Số lượng");
            csv.AppendLine($"Đã gửi;{report.Workflow.Submitted}");
            csv.AppendLine($"Đã duyệt;{report.Workflow.Approved}");
            csv.AppendLine($"Từ chối;{report.Workflow.Rejected}");
            csv.AppendLine($"Đã hủy;{report.Workflow.Cancelled}");
            csv.AppendLine($"Hết hạn giữ giường;{report.Workflow.Expired}");
            csv.AppendLine($"Chờ bổ sung;{report.Workflow.NeedsInformation}");
            csv.AppendLine();
            csv.AppendLine("Sự cố trong kỳ;Số lượng");
            csv.AppendLine($"Đã gửi;{report.Maintenance.Created}");
            csv.AppendLine($"Đã giải quyết;{report.Maintenance.Resolved}");
            csv.AppendLine($"Đang mở;{report.Maintenance.Open}");
            csv.AppendLine($"Khẩn cấp;{report.Maintenance.Urgent}");
            csv.AppendLine($"Chỗ tạm khóa;{report.Maintenance.BlockedBeds}");
        }

        var period = hasPeriod ? report.PeriodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture) : "suc-chua";
        var fileName = $"bao-cao-van-hanh-{period}-{report.Query.Building ?? "toan-ktx"}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", fileName);
    }

    private static string Csv(string value)
    {
        var safe = value.Length > 0 && "=+-@".Contains(value[0]) ? $"'{value}" : value;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
