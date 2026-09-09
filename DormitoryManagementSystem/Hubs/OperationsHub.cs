using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DormitoryManagementSystem.Hubs;

[Authorize]
public sealed class OperationsHub : Hub;
