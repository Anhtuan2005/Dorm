using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class ExpiredRoomHolds(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ExpiredRoomHolds> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<DormitoryDbContext>();
                var recipients = new List<string?>();
                var error = await WorkTransaction.RunAsync(db, async () => {
                    recipients.Clear();
                    var now = clock.GetUtcNow().UtcDateTime;
                    var rows = await db.HousingRequests.Include(x => x.StudentProfile).Include(x => x.ApprovedRoomHold)
                        .Where(x => HousingRequestWorkflow.ActiveStatuses.Contains(x.Status) && x.ApprovedRoomHold != null &&
                            x.ApprovedRoomHold.ReleasedAt == null &&
                            x.ApprovedRoomHold.ExpiresAt <= now).OrderBy(x => x.Id).Take(100).ToListAsync(stoppingToken);
                    foreach (var row in rows)
                    {
                        row.ApprovedRoomHold!.ReleasedAt = now;
                        var note = "Hết hạn giữ tạm. Đơn vẫn chờ xử lý. Ban quản lý sẽ kiểm tra lại giường khi duyệt.";
                        if (row.Status == HousingRequestStatus.AwaitingMoveIn)
                        {
                            row.Status = HousingRequestStatus.Expired;
                            row.ActionRequiredAt = null;
                            row.DecisionReason = note = "Đã hết hạn giữ giường. Bạn có thể gửi đơn chọn phòng mới.";
                        }
                        row.Events.Add(new() { Status = row.Status, Note = note, CreatedAt = now });
                        recipients.Add(row.StudentProfile.UserId);
                        if (row.StudentProfile.UserId is string recipient)
                            db.Notifications.Add(new() { UserId = recipient, Title = "Hết hạn giữ giường", Body = row.Code + " · " + note,
                                TargetUrl = "/Student/Applications", Type = "Application", CreatedAt = now });
                    }
                    return null;
                }, stoppingToken);
                if (error == null && recipients.Count > 0)
                    await scope.ServiceProvider.GetRequiredService<RealtimeUpdates>().NotifyOperationsAsync(recipients.ToArray());
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError(ex, "Không thể cập nhật đơn giữ giường đã hết hạn."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
