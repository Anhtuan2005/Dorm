using DormitoryManagementSystem.Models;

namespace DormitoryManagementSystem.Services;

public sealed record RoomCapacitySnapshot(
    int Capacity,
    int Occupied,
    int Held,
    int Blocked,
    int Assignable)
{
    public bool CanAssign => Assignable > 0;

    public string AvailabilityReason => Blocked > 0
        ? $"{Blocked} chỗ tạm khóa do sự cố"
        : Held > 0 && Assignable == 0
            ? $"{Held} chỗ đang được giữ"
            : Assignable == 0
                ? "Phòng đã đủ sức chứa"
                : $"Còn {Assignable} chỗ có thể xếp";
}

public sealed record RoomCapacityDecision(
    bool Allowed,
    string ErrorMessage,
    RoomCapacitySnapshot Snapshot);

public sealed record MaintenanceBlockDecision(
    bool Allowed,
    int MaximumBlockedBeds,
    string ErrorMessage,
    RoomCapacitySnapshot Snapshot);

public static class RoomCapacityPolicy
{
    private static readonly MaintenanceStatus[] OpenMaintenanceStatuses =
        [MaintenanceStatus.New, MaintenanceStatus.Assigned, MaintenanceStatus.InProgress, MaintenanceStatus.WaitingParts];

    public static RoomCapacitySnapshot FromRoom(Room room, DateTime nowUtc)
    {
        var occupied = room.Stays.Count(x => x.EndedAt is null);
        var held = room.Holds
            .Where(x => x.ReleasedAt is null && x.ExpiresAt > nowUtc)
            .Sum(x => x.HeldBeds);
        var blocked = room.MaintenanceRequests
            .Where(x => OpenMaintenanceStatuses.Contains(x.Status))
            .Sum(x => x.BlockedBeds);

        return Calculate(room.Capacity, occupied, held, blocked);
    }

    public static RoomCapacitySnapshot Calculate(int capacity, int occupied, int held, int blocked)
    {
        capacity = Math.Max(0, capacity);
        occupied = Math.Clamp(occupied, 0, capacity);
        blocked = Math.Clamp(blocked, 0, capacity - occupied);
        held = Math.Clamp(held, 0, capacity - occupied - blocked);
        return new(capacity, occupied, held, blocked, capacity - occupied - blocked - held);
    }

    public static RoomCapacityDecision CanReserve(Room room, int requestedBeds, DateTime nowUtc)
    {
        var snapshot = FromRoom(room, nowUtc);
        if (requestedBeds <= 0)
            return new(false, "Số chỗ cần giữ phải lớn hơn 0.", snapshot);
        return requestedBeds <= snapshot.Assignable
            ? new(true, string.Empty, snapshot)
            : new(false, $"Phòng {room.Code} chỉ còn {snapshot.Assignable} chỗ có thể xếp.", snapshot);
    }

    public static RoomCapacityDecision CanConsumeHold(
        Room room,
        RoomHold? hold,
        int requiredBeds,
        DateTime nowUtc)
    {
        var snapshot = FromRoom(room, nowUtc);
        if (hold is null)
            return new(false, "Đơn không còn thông tin giữ chỗ.", snapshot);
        if (hold.ReleasedAt is not null)
            return new(false, "Chỗ giữ đã được giải phóng.", snapshot);
        if (hold.ExpiresAt <= nowUtc)
            return new(false, "Chỗ giữ đã hết hạn.", snapshot);
        if (hold.RoomId != room.Id)
            return new(false, "Chỗ giữ không thuộc phòng đích.", snapshot);
        if (requiredBeds <= 0 || hold.HeldBeds != requiredBeds)
            return new(false, $"Chỗ giữ không giữ đúng {requiredBeds} chỗ.", snapshot);

        var occupied = room.Stays.Count(x => x.EndedAt is null);
        var otherHeld = room.Holds
            .Where(x => x.Id != hold.Id && x.ReleasedAt is null && x.ExpiresAt > nowUtc)
            .Sum(x => x.HeldBeds);
        var blocked = room.MaintenanceRequests
            .Where(x => IsMaintenanceOpen(x.Status))
            .Sum(x => x.BlockedBeds);
        if (occupied + otherHeld + blocked + requiredBeds > room.Capacity)
            return new(false, $"Phòng {room.Code} không còn đủ sức chứa để tiêu thụ chỗ đã giữ.", snapshot);

        return new(true, string.Empty, snapshot);
    }

    public static MaintenanceBlockDecision CanSetBlockedBeds(
        Room room,
        int currentMaintenanceRequestId,
        int requestedBlockedBeds,
        DateTime nowUtc)
    {
        var occupied = room.Stays.Count(x => x.EndedAt is null);
        var held = room.Holds
            .Where(x => x.ReleasedAt is null && x.ExpiresAt > nowUtc)
            .Sum(x => x.HeldBeds);
        var otherBlocked = room.MaintenanceRequests
            .Where(x => x.Id != currentMaintenanceRequestId && IsMaintenanceOpen(x.Status))
            .Sum(x => x.BlockedBeds);
        var maximum = MaximumBlockedBeds(room.Capacity, occupied, held, otherBlocked);
        var snapshot = Calculate(room.Capacity, occupied, held, otherBlocked + Math.Max(0, requestedBlockedBeds));
        if (requestedBlockedBeds < 0)
            return new(false, maximum, "Số chỗ tạm khóa không thể âm.", snapshot);
        return requestedBlockedBeds <= maximum
            ? new(true, maximum, string.Empty, snapshot)
            : new(false, maximum, $"Phòng {room.Code} chỉ còn tối đa {maximum} chỗ có thể tạm khóa.", snapshot);
    }

    public static int MaximumBlockedBeds(int capacity, int occupied, int held, int otherBlocked) =>
        Math.Max(0, capacity - Math.Max(0, occupied) - Math.Max(0, held) - Math.Max(0, otherBlocked));

    public static bool IsMaintenanceOpen(MaintenanceStatus status) =>
        OpenMaintenanceStatuses.Contains(status);
}
