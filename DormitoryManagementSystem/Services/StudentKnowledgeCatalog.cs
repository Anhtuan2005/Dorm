using System.Globalization;
using System.Text;
using System.Text.Json;
using DormitoryManagementSystem.ViewModels;

namespace DormitoryManagementSystem.Services;

public sealed class StudentKnowledgeCatalog
{
    private const int MaximumFileSize = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };
    private readonly Dictionary<string, ChatbotAnswer> answers = new(StringComparer.Ordinal);

    public StudentKnowledgeCatalog(Stream? approved, bool includeDemo)
    {
        if (approved is not null) AddEntries(approved, isDemo: false);
        if (includeDemo)
        {
            using var demo = typeof(StudentKnowledgeCatalog).Assembly.GetManifestResourceStream("StudentHelpDemo");
            if (demo is not null) AddEntries(demo, isDemo: true);
        }
    }

    internal static StudentKnowledgeCatalog Load(IWebHostEnvironment environment, ILogger<StudentKnowledgeCatalog> logger)
    {
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "chatbot-knowledge.json");
        try
        {
            if (File.Exists(path))
            {
                using var approved = File.OpenRead(path);
                if (approved.Length <= MaximumFileSize)
                    return new StudentKnowledgeCatalog(approved, environment.IsDevelopment());
                logger.LogWarning("The approved student-help catalog exceeds the supported size.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning("The approved student-help catalog could not be loaded ({ErrorType}).", exception.GetType().Name);
        }

        return new StudentKnowledgeCatalog(null, environment.IsDevelopment());
    }

    internal ChatbotAnswer? Find(string normalizedQuestion) => answers.GetValueOrDefault(normalizedQuestion);

    private void AddEntries(Stream source, bool isDemo)
    {
        var entries = JsonSerializer.Deserialize<KnowledgeEntry?[]>(source, JsonOptions) ?? [];
        foreach (var entry in entries)
        {
            if (entry is not { Questions: { Length: > 0 }, Document: { Length: <= 160 } document,
                    Section: { Length: <= 160 } section, Answer: { Length: <= 3000 } text }
                || string.IsNullOrWhiteSpace(document) || string.IsNullOrWhiteSpace(section)
                || string.IsNullOrWhiteSpace(text) || (!isDemo && string.IsNullOrWhiteSpace(entry.ApprovalReference)))
                continue;

            var url = Uri.TryCreate(entry.Url, UriKind.Absolute, out var link)
                && link.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(link.UserInfo) ? link.AbsoluteUri : null;
            var action = entry.Action is "rooms" or "applications" or "invoices" or "repairs" or "notifications" or "password"
                ? entry.Action : null;
            var answer = new ChatbotAnswer(true, text.Trim(), Source: new(document.Trim(), section.Trim(), isDemo, url), Action: action);
            foreach (var question in entry.Questions)
            {
                if (question is not { Length: > 0 and <= 1000 }) continue;
                var normalized = Normalize(question);
                if (normalized.Length > 0) answers.TryAdd(normalized, answer);
            }
        }
    }

    internal static string Normalize(string text)
    {
        var normalized = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
                normalized.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
            else if (normalized.Length > 0 && normalized[^1] != ' ')
                normalized.Append(' ');
        }
        return normalized.ToString().TrimEnd();
    }

    private sealed record KnowledgeEntry(
        string?[]? Questions,
        string? Answer,
        string? Document,
        string? Section,
        string? ApprovalReference,
        string? Url,
        string? Action);
}
