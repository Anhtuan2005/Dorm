using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.ViewComponents;

public sealed class OperationsAttentionBadgeViewComponent(
    IApplicationManagementService applications,
    IMaintenanceManagementService maintenance,
    DormitoryDbContext db,
    TimeProvider timeProvider) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var requestCount = await applications.GetActiveCountAsync(HttpContext.RequestAborted);
        var maintenanceCount = await maintenance.GetAttentionCountAsync(HttpContext.RequestAborted);
        var businessDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime.AddHours(7));
        var overdueInvoiceCount = await db.Invoices
            .AsNoTracking()
            .Where(x => x.Status == InvoiceStatus.Issued && x.BillingPeriod.DueDate < businessDate)
            .CountAsync(x => x.TotalAmount >
                (x.Transactions.Sum(transaction => (decimal?)transaction.Amount) ?? 0m),
                HttpContext.RequestAborted);
        return Content((requestCount + maintenanceCount + overdueInvoiceCount).ToString());
    }
}
