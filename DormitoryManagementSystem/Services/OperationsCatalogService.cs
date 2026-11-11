using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class OperationsCatalogService(DormitoryDbContext db, TimeProvider timeProvider) : IOperationsCatalogService
{
    public async Task<RoomCatalogViewModel> GetAsync(RoomCatalogQuery query, CancellationToken cancellationToken = default)
    {
        var buildingRows = await db.Buildings.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.Name, x.SiteName, x.SortOrder, FloorCount = x.Floors.Count, RoomCount = x.Floors.SelectMany(f => f.Rooms).Count() })
            .ToListAsync(cancellationToken);
        var selectedBuildingId = buildingRows.Any(x => x.Id == query.BuildingId) ? query.BuildingId : buildingRows.FirstOrDefault()?.Id;
        query.BuildingId = selectedBuildingId;
        var floorRows = selectedBuildingId.HasValue
            ? await db.Floors.AsNoTracking().Where(x => x.BuildingId == selectedBuildingId).OrderBy(x => x.Number)
                .Select(x => new { x.Id, x.Number, RoomCount = x.Rooms.Count }).ToListAsync(cancellationToken)
            : [];
        var selectedFloorId = floorRows.Any(x => x.Id == query.FloorId) ? query.FloorId : floorRows.FirstOrDefault()?.Id;
        query.FloorId = selectedFloorId;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rooms = selectedFloorId.HasValue
            ? await db.Rooms.AsNoTracking().Where(x => x.FloorId == selectedFloorId)
                .OrderBy(x => x.Code)
                .Select(x => new CatalogRoomViewModel(
                    x.Id,
                    x.Code,
                    x.RoomType,
                    x.Amenities,
                    x.Capacity,
                    x.Stays.Count(s => s.EndedAt == null),
                    x.Holds.Where(h => h.ReleasedAt == null && h.ExpiresAt > now).Sum(h => (int?)h.HeldBeds) ?? 0,
                    x.MaintenanceRequests.Where(m => m.Status != MaintenanceStatus.Resolved && m.Status != MaintenanceStatus.Closed).Sum(m => (int?)m.BlockedBeds) ?? 0,
                    Math.Max(0, x.Capacity
                        - x.Stays.Count(s => s.EndedAt == null)
                        - (x.Holds.Where(h => h.ReleasedAt == null && h.ExpiresAt > now).Sum(h => (int?)h.HeldBeds) ?? 0)
                        - (x.MaintenanceRequests.Where(m => m.Status != MaintenanceStatus.Resolved && m.Status != MaintenanceStatus.Closed).Sum(m => (int?)m.BlockedBeds) ?? 0)),
                    x.MonthlyPrice,
                    Convert.ToBase64String(x.RowVersion)))
                .ToListAsync(cancellationToken)
            : [];

        return new RoomCatalogViewModel
        {
            Query = query,
            Buildings = buildingRows.Select(x => new CatalogBuildingViewModel(x.Id, x.Code, x.Name, x.SiteName, x.SortOrder, x.FloorCount, x.RoomCount, x.Id == selectedBuildingId)).ToList(),
            Floors = floorRows.Select(x => new CatalogFloorViewModel(x.Id, x.Number, x.RoomCount, x.Id == selectedFloorId)).ToList(),
            Rooms = rooms
        };
    }

    public async Task<CatalogOperationResult> CreateBuildingAsync(CreateBuildingInput input, CancellationToken cancellationToken = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.Buildings.AnyAsync(x => x.Code == code, cancellationToken)) return new(false, $"Mã tòa {code} đã tồn tại.");
        var building = new Building { Code = code, Name = input.Name.Trim(), SiteName = input.SiteName.Trim(), SortOrder = input.SortOrder };
        db.Buildings.Add(building);
        await db.SaveChangesAsync(cancellationToken);
        return new(true, $"Đã tạo {building.Name}.", building.Id);
    }

    public async Task<CatalogOperationResult> UpdateBuildingAsync(UpdateBuildingInput input, CancellationToken cancellationToken = default)
    {
        var building = await db.Buildings.FindAsync([input.Id], cancellationToken);
        if (building is null) return new(false, "Không tìm thấy tòa nhà.");
        building.Name = input.Name.Trim();
        building.SiteName = input.SiteName.Trim();
        building.SortOrder = input.SortOrder;
        await db.SaveChangesAsync(cancellationToken);
        return new(true, $"Đã cập nhật tòa {building.Code}.", building.Id);
    }

    public async Task<CatalogOperationResult> CreateFloorAsync(CreateFloorInput input, CancellationToken cancellationToken = default)
    {
        var building = await db.Buildings.FindAsync([input.BuildingId], cancellationToken);
        if (building is null) return new(false, "Không tìm thấy tòa nhà.");
        if (await db.Floors.AnyAsync(x => x.BuildingId == input.BuildingId && x.Number == input.Number, cancellationToken))
            return new(false, $"Tầng {input.Number} đã tồn tại trong tòa {building.Code}.", building.Id);
        var floor = new Floor { BuildingId = building.Id, Number = input.Number };
        db.Floors.Add(floor);
        await db.SaveChangesAsync(cancellationToken);
        return new(true, $"Đã thêm tầng {floor.Number} vào tòa {building.Code}.", building.Id, floor.Id);
    }

    public async Task<CatalogOperationResult> CreateRoomAsync(CreateRoomInput input, CancellationToken cancellationToken = default)
    {
        var floor = await db.Floors.Include(x => x.Building).SingleOrDefaultAsync(x => x.Id == input.FloorId, cancellationToken);
        if (floor is null) return new(false, "Không tìm thấy tầng.");
        var code = input.Code.Trim().ToUpperInvariant();
        if (!code.StartsWith($"{floor.Building.Code}-", StringComparison.OrdinalIgnoreCase))
            return new(false, $"Mã phòng phải bắt đầu bằng {floor.Building.Code}-.", floor.BuildingId, floor.Id);
        if (await db.Rooms.AnyAsync(x => x.Code == code, cancellationToken)) return new(false, $"Mã phòng {code} đã tồn tại.", floor.BuildingId, floor.Id);
        var room = new Room { FloorId = floor.Id, Code = code, RoomType = input.RoomType.Trim(), Amenities = input.Amenities.Trim(), Capacity = input.Capacity, MonthlyPrice = input.MonthlyPrice };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(cancellationToken);
        return new(true, $"Đã tạo phòng {room.Code}.", floor.BuildingId, floor.Id);
    }

    public async Task<CatalogOperationResult> UpdateRoomAsync(UpdateRoomInput input, CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.Include(x => x.Floor).ThenInclude(x => x.Building)
            .Include(x => x.Stays).Include(x => x.Holds).Include(x => x.MaintenanceRequests)
            .SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken);
        if (room is null) return new(false, "Không tìm thấy phòng.");
        if (room.FloorId != input.FloorId) return new(false, "Không chuyển phòng sang tầng khác trong thao tác chỉnh sửa.", room.Floor.BuildingId, room.FloorId);
        if (!HasCurrentVersion(room, input.RowVersion)) return new(false, "Phòng đã thay đổi. Hãy tải lại trước khi lưu.", room.Floor.BuildingId, room.FloorId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var committed = room.Stays.Count(x => x.EndedAt == null)
            + room.Holds.Where(x => x.ReleasedAt == null && x.ExpiresAt > now).Sum(x => x.HeldBeds)
            + room.MaintenanceRequests.Where(x => x.Status != MaintenanceStatus.Resolved && x.Status != MaintenanceStatus.Closed).Sum(x => x.BlockedBeds);
        if (input.Capacity < committed) return new(false, $"Sức chứa mới thấp hơn {committed} chỗ đang dùng/giữ/khóa.", room.Floor.BuildingId, room.FloorId);
        var code = input.Code.Trim().ToUpperInvariant();
        if (!code.StartsWith($"{room.Floor.Building.Code}-", StringComparison.OrdinalIgnoreCase)) return new(false, $"Mã phòng phải bắt đầu bằng {room.Floor.Building.Code}-.", room.Floor.BuildingId, room.FloorId);
        if (await db.Rooms.AnyAsync(x => x.Id != room.Id && x.Code == code, cancellationToken)) return new(false, $"Mã phòng {code} đã tồn tại.", room.Floor.BuildingId, room.FloorId);
        room.Code = code;
        room.RoomType = input.RoomType.Trim();
        room.Amenities = input.Amenities.Trim();
        room.Capacity = input.Capacity;
        room.MonthlyPrice = input.MonthlyPrice;
        db.Entry(room).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(input.RowVersion);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(true, $"Đã cập nhật phòng {room.Code}.", room.Floor.BuildingId, room.FloorId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "Phòng vừa được người khác cập nhật. Hãy tải lại.", room.Floor.BuildingId, room.FloorId);
        }
    }

    public async Task<CatalogOperationResult> DeleteRoomAsync(int roomId, CancellationToken cancellationToken = default)
    {
        var room = await db.Rooms.Include(x => x.Floor).Include(x => x.Stays).Include(x => x.Holds).Include(x => x.MaintenanceRequests)
            .SingleOrDefaultAsync(x => x.Id == roomId, cancellationToken);
        if (room is null) return new(false, "Không tìm thấy phòng.");
        if (room.Stays.Count != 0 || room.Holds.Count != 0 || room.MaintenanceRequests.Count != 0 || await db.HousingRequests.AnyAsync(x => x.TargetRoomId == room.Id, cancellationToken))
            return new(false, "Không thể xóa phòng đã có lượt ở, giữ chỗ, đơn hoặc sự cố. Hãy giữ bản ghi để bảo toàn lịch sử.", room.Floor.BuildingId, room.FloorId);
        db.Rooms.Remove(room);
        await db.SaveChangesAsync(cancellationToken);
        return new(true, $"Đã xóa phòng {room.Code} chưa phát sinh nghiệp vụ.", room.Floor.BuildingId, room.FloorId);
    }

    private static bool HasCurrentVersion(Room room, string rowVersion)
    {
        try { return room.RowVersion.SequenceEqual(Convert.FromBase64String(rowVersion)); }
        catch (FormatException) { return false; }
    }
}
