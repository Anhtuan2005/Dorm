using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.ViewModels;

public sealed record RoomIllustration(string File, string Caption)
{
    private static readonly RoomIllustration[] BedPhotos =
    [
        new("pondicherry", "Lối đi giữa các giường tầng"),
        new("curtained-bunks", "Giường tầng có rèm riêng"),
        new("formosa", "Cách bố trí hai dãy giường"),
        new("bunk-ladder", "Lối lên giường tầng trên")
    ];

    private static readonly (string Amenity, RoomIllustration Photo)[] AmenityPhotos =
    [
        (RoomAmenities.Bathroom, new("bathroom", "Bồn rửa và khu vệ sinh")),
        ("Bàn học", new("study-desk", "Bàn học cạnh cửa sổ")),
        ("Tủ cá nhân", new("personal-lockers", "Dãy tủ cá nhân bằng kim loại")),
        ("Tủ cá nhân", new("bunk-drawers", "Ngăn kéo dưới giường")),
        ("Quạt đứng", new("standing-fan", "Quạt đứng cạnh cửa sổ")),
        ("Quạt trần", new("ceiling-fan", "Quạt trần")),
        ("Điều hòa", new("air-conditioner", "Điều hòa treo tường")),
        ("Tủ lạnh", new("mini-fridge", "Các ngăn bên trong tủ lạnh mini"))
    ];

    public static RoomIllustration CoverFor(int roomId) => BedPhotos[roomId % BedPhotos.Length];

    public static IReadOnlyList<RoomIllustration> ForRoom(int roomId, IReadOnlyList<string> amenities)
    {
        var nextBedPhoto = (roomId % BedPhotos.Length + 1) % BedPhotos.Length;
        var photos = new List<RoomIllustration>
        {
            CoverFor(roomId), BedPhotos[nextBedPhoto]
        };
        foreach (var (amenity, photo) in AmenityPhotos)
        {
            if (amenities.Contains(amenity, StringComparer.OrdinalIgnoreCase))
                photos.Add(photo);
        }
        return photos;
    }
}
