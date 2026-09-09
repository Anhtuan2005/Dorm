using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public interface IRoomMapService
{
    Task<RoomMapViewModel> GetAsync(RoomMapQuery query, CancellationToken cancellationToken = default);
}
