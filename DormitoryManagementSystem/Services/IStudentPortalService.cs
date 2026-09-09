using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public interface IStudentPortalService
{
    Task<StudentDashboardViewModel> GetDashboardAsync(string userId, CancellationToken cancellationToken = default);
    Task<StudentRoomSearchViewModel> SearchRoomsAsync(string userId, StudentRoomSearchQuery query, CancellationToken cancellationToken = default);
    Task<StudentApplicationsViewModel> GetApplicationsAsync(string userId, CancellationToken cancellationToken = default);
    Task<StudentOperationResult> SubmitHousingRequestAsync(string userId, SubmitHousingRequestInput input, CancellationToken cancellationToken = default);
    Task<StudentOperationResult> CancelHousingRequestAsync(string userId, long requestId, CancellationToken cancellationToken = default);
    Task<StudentInvoicesViewModel> GetInvoicesAsync(string userId, CancellationToken cancellationToken = default);
    Task<StudentRepairsViewModel> GetRepairsAsync(string userId, CancellationToken cancellationToken = default);
    Task<StudentOperationResult> CreateRepairAsync(string userId, CreateRepairRequestInput input, CancellationToken cancellationToken = default);
}
