using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using Xunit;

namespace DormitoryManagementSystem.Tests;

public sealed class MeterAllocationTests
{
    [Fact]
    public void Transfers_split_each_rooms_cost_by_time_and_conserve_whole_dong()
    {
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var middle = start.AddDays(1);
        var end = start.AddDays(2);
        var first = new Room { Id = 11, Code = "A-101" };
        var second = new Room { Id = 22, Code = "B-101" };
        var stays = new[] {
            new Stay { StudentProfileId = 1, RoomId = first.Id, Room = first, StartedAt = start.AddDays(-20), EndedAt = middle },
            new Stay { StudentProfileId = 2, RoomId = first.Id, Room = first, StartedAt = start, EndedAt = end.AddDays(20) },
            new Stay { StudentProfileId = 1, RoomId = second.Id, Room = second, StartedAt = middle },
            new Stay { StudentProfileId = 3, RoomId = second.Id, Room = second, StartedAt = middle }
        };
        var readings = new Dictionary<int, RoomMeterReading> {
            [first.Id] = new() { PowerEnd = 1, PowerRate = 100 },
            [second.Id] = new() { WaterStart = 12, WaterEnd = 13, WaterRate = 101 }
        };

        var split = MeterAllocation.Calculate(stays, readings, start, end);

        Assert.Equal(33m, split[1].Single(x => x.ChargeType == "Điện").Amount);
        Assert.Equal(67m, Assert.Single(split[2]).Amount);
        Assert.Equal(51m, split[1].Single(x => x.ChargeType == "Nước").Amount);
        Assert.Equal(50m, Assert.Single(split[3]).Amount);
        Assert.Equal(201m, split.SelectMany(group => group).Sum(line => line.Amount));
    }
}
