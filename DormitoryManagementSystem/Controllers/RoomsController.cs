using DormitoryManagementSystem.Security;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DormitoryManagementSystem.Controllers;

[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.DormManager}")]
public sealed class RoomsController(IRoomMapService roomMapService, IOperationsCatalogService catalogService) : Controller
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

    [HttpGet]
    public async Task<IActionResult> Catalog([FromQuery] RoomCatalogQuery query, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Danh mục phòng";
        ViewData["ActiveNav"] = "rooms";
        return View(await catalogService.GetAsync(query, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBuilding(CreateBuildingInput input, CancellationToken cancellationToken) =>
        RedirectWith(ModelState.IsValid ? await catalogService.CreateBuildingAsync(input, cancellationToken) : InvalidCatalog());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBuilding(UpdateBuildingInput input, CancellationToken cancellationToken) =>
        RedirectWith(ModelState.IsValid ? await catalogService.UpdateBuildingAsync(input, cancellationToken) : InvalidCatalog(input.Id));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFloor(CreateFloorInput input, CancellationToken cancellationToken) =>
        RedirectWith(ModelState.IsValid ? await catalogService.CreateFloorAsync(input, cancellationToken) : InvalidCatalog(input.BuildingId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoom(CreateRoomInput input, CancellationToken cancellationToken) =>
        RedirectWith(ModelState.IsValid ? await catalogService.CreateRoomAsync(input, cancellationToken) : InvalidCatalog(floorId: input.FloorId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRoom(UpdateRoomInput input, CancellationToken cancellationToken) =>
        RedirectWith(ModelState.IsValid ? await catalogService.UpdateRoomAsync(input, cancellationToken) : InvalidCatalog(floorId: input.FloorId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoom(int roomId, CancellationToken cancellationToken) =>
        RedirectWith(await catalogService.DeleteRoomAsync(roomId, cancellationToken));

    private CatalogOperationResult InvalidCatalog(int? buildingId = null, int? floorId = null) =>
        new(false, ModelState.Values.SelectMany(x => x.Errors).FirstOrDefault()?.ErrorMessage ?? "Dữ liệu chưa hợp lệ.", buildingId, floorId);

    private IActionResult RedirectWith(CatalogOperationResult result)
    {
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Catalog), new { result.BuildingId, result.FloorId });
    }
}
