using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.ViewModels;

public sealed record OperationsNotificationsViewModel(
    IReadOnlyList<Notification> Items, bool UnreadOnly, int UnreadCount, int Page, int TotalPages);
