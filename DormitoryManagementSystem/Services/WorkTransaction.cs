using System.Data;
using DormitoryManagementSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

internal static class WorkTransaction
{
    public static async Task<string?> RunAsync(DormitoryDbContext db, Func<Task<string?>> work, CancellationToken token)
    {
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
                var error = await work();
                if (error != null) return error;
                await db.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                return null;
            });
        }
        catch (DbUpdateConcurrencyException) { return "Dữ liệu vừa thay đổi. Vui lòng tải lại trước khi lưu."; }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return "Thông tin đã được ghi nhận. Vui lòng kiểm tra lại danh sách."; }
    }
}
