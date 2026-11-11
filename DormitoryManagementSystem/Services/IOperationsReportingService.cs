using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public interface IOperationsReportingService
{
    Task<ManagerDashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<OperationsReportViewModel> GetReportAsync(OperationsReportQuery query, CancellationToken cancellationToken = default);
    Task<ManagerSettingsViewModel> GetSettingsAsync(CancellationToken cancellationToken = default);
}
