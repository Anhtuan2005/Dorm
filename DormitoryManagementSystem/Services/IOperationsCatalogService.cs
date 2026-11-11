using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public interface IOperationsCatalogService
{
    Task<RoomCatalogViewModel> GetAsync(RoomCatalogQuery query, CancellationToken cancellationToken = default);
    Task<CatalogOperationResult> CreateBuildingAsync(CreateBuildingInput input, CancellationToken cancellationToken = default);
    Task<CatalogOperationResult> UpdateBuildingAsync(UpdateBuildingInput input, CancellationToken cancellationToken = default);
    Task<CatalogOperationResult> CreateFloorAsync(CreateFloorInput input, CancellationToken cancellationToken = default);
    Task<CatalogOperationResult> CreateRoomAsync(CreateRoomInput input, CancellationToken cancellationToken = default);
    Task<CatalogOperationResult> UpdateRoomAsync(UpdateRoomInput input, CancellationToken cancellationToken = default);
    Task<CatalogOperationResult> DeleteRoomAsync(int roomId, CancellationToken cancellationToken = default);
}
