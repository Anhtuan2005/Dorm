using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DormitoryManagementSystem.IntegrationTests;

public sealed class MaintenanceCapacityIntegrationTests
{
    [SqlServerFact]
    public async Task Blocking_beds_cannot_consume_an_active_hold()
    {
        var now = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);
        await using var database = await SqlServerTestDatabase.CreateAsync();
        int requestId;
        string rowVersion;
        await using (var setup = database.CreateContext())
        {
            var building = new Building { Code = "QB", Name = "Tòa QB", SiteName = "Cơ sở kiểm thử", SortOrder = 1 };
            var floor = new Floor { Number = 1, Building = building };
            var room = new Room { Code = "QB-101", Floor = floor, RoomType = "Nam", Capacity = 8, MonthlyPrice = 1_000_000m };
            for (var index = 0; index < 6; index++)
            {
                setup.StudentProfiles.Add(new StudentProfile
                {
                    StudentCode = $"SV-QB-{index:000}",
                    FullName = $"Sinh viên {index}",
                    Gender = "Nam",
                    Faculty = "Kiểm thử",
                    PhoneNumber = $"09000000{index:00}",
                    Stays = [new Stay { Room = room, StartedAt = now.AddMonths(-1) }]
                });
            }
            setup.RoomHolds.Add(new RoomHold { Room = room, HeldBeds = 1, ExpiresAt = now.AddHours(2) });
            var request = new MaintenanceRequest
            {
                Room = room,
                Code = "SC-QB-0001",
                Title = "Kiểm tra sức chứa",
                Category = "Thiết bị phòng",
                Description = "Sự cố dùng để kiểm tra chỗ tạm khóa.",
                Priority = MaintenancePriority.Medium,
                Status = MaintenanceStatus.InProgress,
                AssignedTeam = "Tổ Thiết bị",
                CreatedAt = now.AddHours(-1),
                UpdatedAt = now.AddHours(-1)
            };
            setup.MaintenanceRequests.Add(request);
            await setup.SaveChangesAsync();
            requestId = request.Id;
            rowVersion = Convert.ToBase64String(request.RowVersion);
        }

        await using var context = database.CreateContext();
        var result = await new MaintenanceManagementService(context, new FixedTimeProvider(new DateTimeOffset(now)))
            .UpdateAsync(new UpdateMaintenanceInput
            {
                RequestId = requestId,
                Status = MaintenanceStatus.InProgress.ToString(),
                Priority = MaintenancePriority.Medium.ToString(),
                AssignedTeam = "Tổ Thiết bị",
                BlockedBeds = 2,
                Note = "Thử khóa hai chỗ đang còn lại.",
                RowVersion = rowVersion
            }, "manager-test");

        Assert.False(result.Success);
        Assert.Contains("tối đa 1", result.Message);
        await using var verify = database.CreateContext();
        Assert.Equal(0, await verify.MaintenanceRequests.Where(x => x.Id == requestId).Select(x => x.BlockedBeds).SingleAsync());
    }
}
