using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.ViewModels;

public sealed class RoomCatalogQuery
{
    public int? BuildingId { get; set; }
    public int? FloorId { get; set; }
}

public sealed record CatalogBuildingViewModel(int Id, string Code, string Name, string SiteName, int SortOrder, int FloorCount, int RoomCount, bool IsSelected);
public sealed record CatalogFloorViewModel(int Id, int Number, int RoomCount, bool IsSelected);

public sealed record CatalogRoomViewModel(
    int Id,
    string Code,
    string RoomType,
    string Amenities,
    int Capacity,
    int Occupied,
    int Held,
    int Blocked,
    int Assignable,
    decimal MonthlyPrice,
    string RowVersion);

public sealed class RoomCatalogViewModel
{
    public required RoomCatalogQuery Query { get; init; }
    public required IReadOnlyList<CatalogBuildingViewModel> Buildings { get; init; }
    public required IReadOnlyList<CatalogFloorViewModel> Floors { get; init; }
    public required IReadOnlyList<CatalogRoomViewModel> Rooms { get; init; }
    public CatalogBuildingViewModel? SelectedBuilding => Buildings.FirstOrDefault(x => x.IsSelected);
    public CatalogFloorViewModel? SelectedFloor => Floors.FirstOrDefault(x => x.IsSelected);
}

public sealed class CreateBuildingInput
{
    [Required, StringLength(4, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(100)] public string SiteName { get; set; } = string.Empty;
    [Range(0, 999)] public int SortOrder { get; set; }
}

public sealed class UpdateBuildingInput
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(100)] public string SiteName { get; set; } = string.Empty;
    [Range(0, 999)] public int SortOrder { get; set; }
}

public sealed class CreateFloorInput
{
    [Range(1, int.MaxValue)] public int BuildingId { get; set; }
    [Range(1, 100)] public int Number { get; set; }
}

public class CreateRoomInput
{
    [Range(1, int.MaxValue)] public int FloorId { get; set; }
    [Required, StringLength(12, MinimumLength = 3)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(24)] public string RoomType { get; set; } = string.Empty;
    [StringLength(240)] public string Amenities { get; set; } = string.Empty;
    [Range(1, 100)] public int Capacity { get; set; }
    [Range(typeof(decimal), "0", "999999999")] public decimal MonthlyPrice { get; set; }
}

public sealed class UpdateRoomInput : CreateRoomInput
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed record CatalogOperationResult(bool Success, string Message, int? BuildingId = null, int? FloorId = null);
