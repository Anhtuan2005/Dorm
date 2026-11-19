using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed record OperationsAttentionCounts(int Applications, int Maintenance, int OverdueInvoices)
{
    public int Total => Applications + Maintenance + OverdueInvoices;
}

public sealed class OperationsAttentionService(
    IApplicationManagementService applications,
    IMaintenanceManagementService maintenance,
    DormitoryDbContext db,
    TimeProvider timeProvider)
{
    public async Task<OperationsAttentionCounts> GetAsync(CancellationToken cancellationToken)
    {
        var requestCount = await applications.GetManagementWorkCountAsync(cancellationToken);
        var maintenanceCount = await maintenance.GetOpenCountAsync(cancellationToken);
        var businessDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime.AddHours(7));
        var overdueInvoiceCount = await db.Invoices.AsNoTracking()
            .Where(x => x.Status == InvoiceStatus.Issued && x.BillingPeriod.DueDate < businessDate)
            .CountAsync(x => x.TotalAmount > (x.Transactions.Sum(entry => (decimal?)entry.Amount) ?? 0m), cancellationToken);
        return new(requestCount, maintenanceCount, overdueInvoiceCount);
    }
}
