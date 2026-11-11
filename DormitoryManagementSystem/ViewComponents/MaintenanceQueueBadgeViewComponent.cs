using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.ViewComponents;

public sealed class MaintenanceQueueBadgeViewComponent(IMaintenanceManagementService service) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        Content((await service.GetAttentionCountAsync(HttpContext.RequestAborted)).ToString());
}
