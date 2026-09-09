using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Services;

public sealed class RoomMapService(DormitoryDbContext db) : IRoomMapService
{
    public async Task<RoomMapViewModel> GetAsync(
        RoomMapQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var rooms = await db.Rooms
            .AsNoTracking()
            .Include(x => x.Floor)
            .ThenInclude(x => x.Building)
            .Include(x => x.Stays.Where(stay => stay.EndedAt == null))
            .ThenInclude(x => x.StudentProfile)
            .Include(x => x.Holds.Where(hold => hold.ReleasedAt == null && hold.ExpiresAt > now))
            .Include(x => x.MaintenanceRequests.Where(request =>
                request.Status != MaintenanceStatus.Resolved &&
                request.Status != MaintenanceStatus.Closed))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        if (rooms.Count == 0)
            throw new InvalidOperationException("Chưa có dữ liệu phòng.");

        var facts = rooms.Select(ToFact).ToList();
        var buildingCodes = facts
            .OrderBy(x => x.Room.Floor.Building.SortOrder)
            .Select(x => x.Room.Floor.Building.Code)
            .Distinct()
            .ToList();
        var selectedBuildingCode = buildingCodes.Contains(query.Building.ToUpperInvariant())
            ? query.Building.ToUpperInvariant()
            : buildingCodes[0];
        query.Building = selectedBuildingCode;

        var buildings = facts
            .GroupBy(x => new
            {
                x.Room.Floor.Building.Code,
                x.Room.Floor.Building.Name,
                x.Room.Floor.Building.SortOrder
            })
            .OrderBy(x => x.Key.SortOrder)
            .Select(group => new BuildingSummaryViewModel(
                group.Key.Code,
                group.Key.Name,
                Sum(group)))
            .ToList();

        var buildingFacts = facts
            .Where(x => x.Room.Floor.Building.Code == selectedBuildingCode)
            .ToList();
        var selectedBuilding = buildingFacts[0].Room.Floor.Building;

        var selectedCode = string.IsNullOrWhiteSpace(query.Room)
            ? $"{selectedBuildingCode}-302"
            : query.Room.ToUpperInvariant();
        var selectedFact = buildingFacts.FirstOrDefault(x => x.Room.Code == selectedCode)
            ?? buildingFacts.First();
        query.Room = selectedFact.Room.Code;

        IEnumerable<RoomFact> filtered = buildingFacts;
        if (query.Floor.HasValue)
            filtered = filtered.Where(x => x.Room.Floor.Number == query.Floor.Value);
        if (!string.IsNullOrWhiteSpace(query.RoomType))
            filtered = filtered.Where(x => x.Room.RoomType == query.RoomType);
        if (query.MaxPrice.HasValue)
            filtered = filtered.Where(x => x.Room.MonthlyPrice <= query.MaxPrice.Value);
        if (query.OnlyAvailable)
            filtered = filtered.Where(x => x.Assignable > 0);
        if (!string.IsNullOrWhiteSpace(query.Search))
            filtered = filtered.Where(x => x.Room.Code.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase));

        filtered = query.Status switch
        {
            "available" => filtered.Where(x => x.Assignable > 0),
            "full" => filtered.Where(x => x.Assignable == 0 && x.Held == 0 && x.Blocked == 0),
            "held" => filtered.Where(x => x.Held > 0),
            "maintenance" => filtered.Where(x => x.Blocked > 0),
            _ => filtered
        };

        var floorMaps = filtered
            .GroupBy(x => x.Room.Floor.Number)
            .OrderByDescending(x => x.Key)
            .Select(group => new FloorMapViewModel(
                group.Key,
                group.OrderBy(x => x.Room.Code)
                    .Select(x => ToCard(x, x.Room.Id == selectedFact.Room.Id))
                    .ToList()))
            .ToList();

        return new RoomMapViewModel
        {
            Query = query,
            GlobalCapacity = Sum(facts),
            SelectedBuildingCapacity = Sum(buildingFacts),
            SelectedBuildingName = selectedBuilding.Name,
            Buildings = buildings,
            Floors = floorMaps,
            RoomTypes = buildingFacts.Select(x => x.Room.RoomType).Distinct().Order().ToList(),
            SelectedRoom = ToDetail(selectedFact)
        };
    }

    private static RoomFact ToFact(Room room)
    {
        var occupied = room.Stays.Count(x => x.EndedAt == null);
        var blocked = Math.Min(
            Math.Max(0, room.Capacity - occupied),
            room.MaintenanceRequests
                .Where(x => x.Status is not MaintenanceStatus.Resolved and not MaintenanceStatus.Closed)
                .Sum(x => x.BlockedBeds));
        var held = Math.Min(
            Math.Max(0, room.Capacity - occupied - blocked),
            room.Holds
                .Where(x => x.ReleasedAt == null && x.ExpiresAt > DateTime.UtcNow)
                .Sum(x => x.HeldBeds));
        var assignable = Math.Max(0, room.Capacity - occupied - blocked - held);
        return new RoomFact(room, occupied, held, assignable, blocked);
    }

    private static CapacityViewModel Sum(IEnumerable<RoomFact> facts)
    {
        var items = facts.ToList();
        return new CapacityViewModel(
            items.Sum(x => x.Room.Capacity),
            items.Sum(x => x.Occupied),
            items.Sum(x => x.Held),
            items.Sum(x => x.Assignable),
            items.Sum(x => x.Blocked));
    }

    private static RoomCardViewModel ToCard(RoomFact fact, bool isSelected)
    {
        var (label, css) = Status(fact);
        return new RoomCardViewModel(
            fact.Room.Id,
            fact.Room.Code,
            fact.Room.Floor.Number,
            fact.Room.RoomType,
            fact.Room.MonthlyPrice,
            fact.Room.Capacity,
            fact.Occupied,
            fact.Held,
            fact.Assignable,
            fact.Blocked,
            label,
            css,
            isSelected);
    }

    private static RoomDetailViewModel ToDetail(RoomFact fact)
    {
        var (label, css) = Status(fact);
        var students = fact.Room.Stays
            .Where(x => x.EndedAt == null)
            .OrderBy(x => x.StudentProfile.StudentCode)
            .Select(x => new StudentSummaryViewModel(x.StudentProfile.StudentCode, x.StudentProfile.FullName))
            .ToList();
        var incidents = fact.Room.MaintenanceRequests
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new IncidentSummaryViewModel(
                x.Code,
                x.Title,
                x.Priority == MaintenancePriority.Urgent ? "Khẩn cấp" : "Ưu tiên cao",
                x.Status == MaintenanceStatus.New ? "Chưa phân công" : "Đang xử lý"))
            .ToList();

        return new RoomDetailViewModel(
            fact.Room.Code,
            fact.Room.RoomType,
            fact.Room.MonthlyPrice,
            new CapacityViewModel(fact.Room.Capacity, fact.Occupied, fact.Held, fact.Assignable, fact.Blocked),
            label,
            css,
            students,
            fact.Room.Amenities.Split('|', StringSplitOptions.RemoveEmptyEntries),
            incidents);
    }

    private static (string Label, string Css) Status(RoomFact fact)
    {
        if (fact.Blocked > 0) return ("Bảo trì · không thể xếp", "maintenance");
        if (fact.Assignable > 0) return ($"Còn {fact.Assignable} chỗ", "available");
        if (fact.Held > 0) return ($"Đang giữ {fact.Held} chỗ", "held");
        return ("Đã đầy", "full");
    }

    private sealed record RoomFact(Room Room, int Occupied, int Held, int Assignable, int Blocked);
}
