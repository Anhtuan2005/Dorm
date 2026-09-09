using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.ViewComponents;

public sealed class ApplicationQueueBadgeViewComponent(IApplicationManagementService service) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        Content((await service.GetActiveCountAsync(HttpContext.RequestAborted)).ToString());
}
