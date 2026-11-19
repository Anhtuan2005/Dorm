namespace DormitoryManagementSystem.ViewModels;

public sealed record RoomPhotoViewModel(long Id, string Caption);

public sealed record RoomPhotoGalleryViewModel(int RoomId, string RoomCode, int FloorId, int BuildingId,
    int MaximumPhotos, IReadOnlyList<RoomPhotoViewModel> Photos);
