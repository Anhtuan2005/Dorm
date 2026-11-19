namespace DormitoryManagementSystem.ViewModels;

public sealed record RoomIllustration(string File, string Caption)
{
    public static IReadOnlyList<RoomIllustration> ForRoom(int roomId, IReadOnlyList<string> amenities)
    {
        var photos = new List<RoomIllustration> { new("bunks", "Lối đi giữa hai dãy giường tầng") };
        if (amenities.Contains("Bàn học", StringComparer.OrdinalIgnoreCase))
            photos.Add(new("study", "Góc bàn học cạnh cửa sổ"));
        if (amenities.Contains("Tủ cá nhân", StringComparer.OrdinalIgnoreCase))
            photos.Add(new("wardrobe", "Tủ cá nhân và giường tầng"));
        if (amenities.Contains("Quạt đứng", StringComparer.OrdinalIgnoreCase))
            photos.Add(new("fan", "Quạt đứng ở cuối dãy giường"));
        var first = roomId % photos.Count;
        return [.. photos.Skip(first), .. photos.Take(first)];
    }
}
