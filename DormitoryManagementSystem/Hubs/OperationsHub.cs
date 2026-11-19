using DormitoryManagementSystem.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DormitoryManagementSystem.Hubs;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager},{RoleNames.Student}")]
public sealed class OperationsHub : Hub
{
    internal const string StaffGroup = "operations-staff";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(RoleNames.Admin) == true || Context.User?.IsInRole(RoleNames.DormManager) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup, Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }
}
