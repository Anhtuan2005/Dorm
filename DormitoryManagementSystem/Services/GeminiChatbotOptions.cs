namespace DormitoryManagementSystem.Services;

public sealed class GeminiChatbotOptions
{
    public const string SectionName = "Chatbot";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.5-flash-lite";
}
