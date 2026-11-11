using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public interface IApplicationManagementService
{
    Task<ApplicationQueueViewModel> GetQueueAsync(ApplicationQueueQuery query, CancellationToken cancellationToken = default);
    Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default);
    Task<ApplicationManagementResult> ExecuteAsync(ApplicationActionInput input, HousingRequestAction action, string managerUserId, CancellationToken cancellationToken = default);
}
