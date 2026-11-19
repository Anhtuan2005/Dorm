using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.ViewComponents;

public sealed class OperationsAttentionBadgeViewComponent(OperationsAttentionService attention) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        Content((await attention.GetAsync(HttpContext.RequestAborted)).Total.ToString());
}
