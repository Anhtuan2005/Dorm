namespace DormitoryManagementSystem.Models;

public static class RoomAmenities
{
    public const string Bathroom = "WC khép kín";
    public const int MaximumStoredLength = 240;
    private static readonly string[] FanTypes = ["Quạt trần", "Quạt đứng"];

    public static IReadOnlyList<string> Optional(string? value) =>
        (value ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.Equals(item, Bathroom, StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => string.Equals(item, "Quạt", StringComparison.OrdinalIgnoreCase)
                ? FanTypes : [item])
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> List(string? value) => [Bathroom, .. Optional(value)];

    public static string Serialize(string? value) => string.Join('|', List(value));
}
