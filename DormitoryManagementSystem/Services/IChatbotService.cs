using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public interface IChatbotService
{
    Task<ChatbotAnswer> AskAsync(string studentUserId, ChatbotQuestion question, CancellationToken cancellationToken = default);
}
