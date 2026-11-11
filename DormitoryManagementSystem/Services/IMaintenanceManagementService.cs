using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public interface IMaintenanceManagementService
{
    Task<MaintenanceQueueViewModel> GetQueueAsync(MaintenanceQueueQuery query, CancellationToken cancellationToken = default);
    Task<MaintenanceOperationResult> UpdateAsync(UpdateMaintenanceInput input, string managerUserId, CancellationToken cancellationToken = default);
    Task<int> GetAttentionCountAsync(CancellationToken cancellationToken = default);
}
