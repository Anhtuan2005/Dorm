using System.Globalization;

namespace DormitoryManagementSystem.Models;

public sealed record RoomAmenityOption(string Name, string ModelKey, params string[] Aliases);

public static class RoomAmenities
{
    public const string Bathroom = "WC khép kín";
    public const int MaximumStoredLength = 240;
    public static IReadOnlyList<RoomAmenityOption> Options { get; } =
    [
        new("Wi-Fi", "wifi", "WiFi", "Wi Fi", "Internet", "Mạng không dây"),
        new("Điều hòa", "air", "Máy lạnh", "AC"),
        new("Quạt trần", "ceilingFan", "Quạt", "Máy quạt"),
        new("Quạt đứng", "standingFan", "Quạt", "Máy quạt", "Quạt cây"),
        new("Bàn học", "desk", "Bàn làm việc"),
        new("Tủ cá nhân", "locker", "Tủ đồ", "Tủ quần áo", "Locker"),
        new("Tủ lạnh", "fridge"),
        new("Bình nóng lạnh", "heater", "Nước nóng", "Máy nước nóng"),
        new("Ban công", "balcony", "Lô gia")
    ];

    private static readonly ILookup<string, string> StoredNames = Options
        .SelectMany(option => option.Aliases.Prepend(option.Name).Select(label => (label, option.Name)))
        .ToLookup(pair => pair.label, pair => pair.Name,
            StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));

    public static string? ModelKey(string name) => name == Bathroom ? "bath" :
        Options.FirstOrDefault(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase))?.ModelKey;

    public static IReadOnlyList<string> Optional(string? value) =>
        (value ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.Equals(item, Bathroom, StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => StoredNames[item].DefaultIfEmpty(item))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> List(string? value) => [Bathroom, .. Optional(value)];

    public static string Serialize(string? value) => string.Join('|', List(value));
}
