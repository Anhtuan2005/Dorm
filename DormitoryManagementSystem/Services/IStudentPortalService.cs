using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public interface IStudentRoomSearch
{
    Task<StudentRoomSearchViewModel> SearchRoomsAsync(string userId, StudentRoomSearchQuery query, CancellationToken cancellationToken = default);
}

public interface IStudentPortalService : IStudentRoomSearch
{
    Task<StudentRoomPreviewViewModel?> GetRoomPreviewAsync(string userId, int roomId, CancellationToken cancellationToken = default);
    Task<StudentDashboardViewModel> GetDashboardAsync(string userId, CancellationToken cancellationToken = default);
    Task<StudentNotificationsViewModel> GetNotificationsAsync(string userId, int page, CancellationToken cancellationToken = default);
    Task<int> GetUnreadNotificationCountAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentNotificationViewModel>> GetRecentNotificationsAsync(string userId, CancellationToken cancellationToken = default);
    Task<string?> ReadNotificationAsync(string userId, long id, CancellationToken cancellationToken = default);
    Task<int> MarkAllNotificationsReadAsync(string userId, CancellationToken cancellationToken = default);
    Task<StudentApplicationsViewModel> GetApplicationsAsync(string userId, CancellationToken cancellationToken = default);
    Task<StudentOperationResult> SubmitHousingRequestAsync(string userId, SubmitHousingRequestInput input, CancellationToken cancellationToken = default);
    Task<StudentOperationResult> CancelHousingRequestAsync(string userId, StudentHousingRequestActionInput input, CancellationToken cancellationToken = default);
    Task<StudentOperationResult> SupplementHousingRequestAsync(string userId, SupplementHousingRequestInput input, CancellationToken cancellationToken = default);
    Task<StudentOperationResult> SubmitCheckoutRequestAsync(string userId, SubmitCheckoutRequestInput input, CancellationToken cancellationToken = default);
    Task<StudentInvoicesViewModel> GetInvoicesAsync(string userId, CancellationToken cancellationToken = default);
    Task<StudentRepairsViewModel> GetRepairsAsync(string userId, CancellationToken cancellationToken = default);
    Task<StudentOperationResult> CreateRepairAsync(string userId, CreateRepairRequestInput input, CancellationToken cancellationToken = default);
    Task<StudentOperationResult> SetRepairFollowingAsync(string userId, int requestId, bool following, CancellationToken cancellationToken = default);
}
