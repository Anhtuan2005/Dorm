using System.Globalization;
using DormitoryManagementSystem.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DormitoryManagementSystem.Services;

public sealed class RealtimeUpdates(IHubContext<OperationsHub> hub, ILogger<RealtimeUpdates> logger)
{
    public Task NotifyStudentsAsync(params string?[] userIds) => PublishAsync(false, userIds);

    public Task NotifyOperationsAsync(params string?[] studentUserIds) => PublishAsync(true, studentUserIds);

    public Task NotifyHousingSupplementAsync(long requestId, string code) =>
        PublishAsync(true, [], (requestId, code));

    public Task NotifyOperationsInboxAsync(string userId) =>
        PublishAsync(false, [], staffInboxUserId: userId);

    public async Task RevokeStudentSessionAsync(string userId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await hub.Clients.User(userId).SendAsync("StudentSessionRevoked", timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Không gửi được tín hiệu kết thúc phiên sinh viên.");
        }
    }

    private async Task PublishAsync(bool notifyStaff, string?[] userIds,
        (long Id, string Code)? supplement = null, string? staffInboxUserId = null)
    {
        var recipients = userIds.OfType<string>().Where(id => id.Length != 0)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (recipients.Length == 0 && !notifyStaff && staffInboxUserId is null) return;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            var updates = new List<Task>();
            if (recipients.Length != 0)
                updates.Add(hub.Clients.Users(recipients).SendAsync("StudentNotificationsChanged", timeout.Token));
            if (notifyStaff)
                updates.Add(hub.Clients.Group(OperationsHub.StaffGroup).SendAsync("OperationsAttentionChanged", timeout.Token));
            if (staffInboxUserId is not null)
                updates.Add(hub.Clients.User(staffInboxUserId).SendAsync("OperationsAttentionChanged", timeout.Token));
            if (supplement is { } reply)
                updates.Add(hub.Clients.Group(OperationsHub.StaffGroup).SendAsync(
                    "HousingRequestSupplemented", reply.Id.ToString(CultureInfo.InvariantCulture), reply.Code, timeout.Token));
            await Task.WhenAll(updates);
        }
        catch (Exception exception)
        {
            // These signals follow the SQL write. A transport failure must not fail a committed action.
            logger.LogWarning(exception, "Không gửi được tín hiệu cập nhật thông báo.");
        }
    }
}
