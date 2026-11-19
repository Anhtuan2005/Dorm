using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.ViewModels;

public sealed class ChatbotQuestion
{
    [Required(ErrorMessage = "Vui lòng nhập câu hỏi.")]
    [StringLength(1000, ErrorMessage = "Câu hỏi không quá 1.000 ký tự.")]
    public string Message { get; set; } = string.Empty;

    [RegularExpression("^(contact|rooms|applications|invoices|repairs|notifications|password)$")]
    public string? PreviousAction { get; set; }

    [Required, MaxLength(6)]
    public List<ChatbotTurn> History { get; set; } = [];
}

public sealed class ChatbotTurn
{
    [Required, RegularExpression("^(user|model)$")]
    public string Role { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Text { get; set; } = string.Empty;
}

public sealed record ChatbotSource(string Document, string Section, bool IsDemo, string? Url = null);

public sealed record ChatbotAnswer(
    bool Success,
    string Text,
    int StatusCode = 200,
    ChatbotSource? Source = null,
    string? Action = null,
    bool UsedGemini = false,
    string? RoomBuilding = null);
