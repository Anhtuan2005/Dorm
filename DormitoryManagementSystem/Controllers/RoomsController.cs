using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
public sealed class RoomsController(IRoomMapService roomMapService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Map(
        [FromQuery] RoomMapQuery query,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Sơ đồ phòng";
        ViewData["ActiveNav"] = "rooms";
        return View(await roomMapService.GetAsync(query, cancellationToken));
    }
}
