using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Services;

public static class RoomBedPolicy
{
    public static IReadOnlyList<int> Available(Room room, DateTime now, int? excludingHoldId = null, int? excludingIncidentId = null)
    {
        var stays = room.Stays.Where(x => x.EndedAt is null).ToList();
        var holds = room.Holds.Where(x => x.Id != excludingHoldId && x.ReleasedAt is null && x.ExpiresAt > now).ToList();
        var incidents = room.MaintenanceRequests
            .Where(x => x.Id != excludingIncidentId && RoomCapacityPolicy.IsMaintenanceOpen(x.Status)).ToList();
        var numbered = stays.Select(x => x.BedNumber)
            .Concat(holds.Select(x => x.BedNumber))
            .OfType<int>().Concat(incidents.SelectMany(x => ParseBlocks(x.BlockedBedNumbers))).ToHashSet();

        var committed = stays.Count + holds.Sum(x => x.HeldBeds) + incidents.Sum(x => x.BlockedBeds);
        // Existing records may reserve capacity without identifying a number. Reserve the first
        // remaining slots for those records before offering any slots for a new request.
        var unnumbered = Math.Max(0, committed - numbered.Count(n => n > 0 && n <= room.Capacity));
        return Enumerable.Range(1, room.Capacity).Where(n => !numbered.Contains(n)).Skip(unnumbered).ToArray();
    }

    public static IReadOnlyList<int> ParseBlocks(string? value) => value is null
        ? []
        : value.Split(',').Select(int.Parse).ToArray();

    public static int HighestCommittedNumber(Room room, DateTime now) =>
        room.Stays.Where(x => x.EndedAt is null).Select(x => x.BedNumber)
            .Concat(room.Holds.Where(x => x.ReleasedAt is null && x.ExpiresAt > now).Select(x => x.BedNumber))
            .OfType<int>()
            .Concat(room.MaintenanceRequests.Where(x => RoomCapacityPolicy.IsMaintenanceOpen(x.Status))
                .SelectMany(x => ParseBlocks(x.BlockedBedNumbers)))
            .DefaultIfEmpty(0).Max();
}
