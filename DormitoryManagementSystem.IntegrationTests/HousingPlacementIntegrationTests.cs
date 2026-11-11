using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DormitoryManagementSystem.IntegrationTests;

public sealed class HousingPlacementIntegrationTests
{
    private static readonly DateTime Now = new(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);

    [SqlServerFact]
    public async Task Expired_hold_cannot_create_a_stay()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var setup = database.CreateContext();
        var scenario = await CreateAwaitingMoveInAsync(setup, Now.AddMinutes(-1));

        await using var context = database.CreateContext();
        var result = await CreateService(context).ExecuteAsync(
            new ApplicationActionInput { Id = scenario.RequestId, RowVersion = scenario.RowVersion },
            HousingRequestAction.ConfirmMoveIn,
            scenario.ManagerId);

        Assert.False(result.Success);
        Assert.Contains("hết hạn", result.Message);
        await using var verify = database.CreateContext();
        var request = await verify.HousingRequests.Include(x => x.ApprovedRoomHold).SingleAsync(x => x.Id == scenario.RequestId);
        Assert.Equal(HousingRequestStatus.AwaitingMoveIn, request.Status);
        Assert.Null(request.ApprovedRoomHold!.ReleasedAt);
        Assert.False(await verify.Stays.AnyAsync(x => x.HousingRequestId == scenario.RequestId));
    }

    [SqlServerFact]
    public async Task Rejecting_an_expired_handoff_releases_the_hold_and_closes_the_request()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var setup = database.CreateContext();
        var scenario = await CreateAwaitingMoveInAsync(setup, Now.AddMinutes(-1));

        await using var context = database.CreateContext();
        var result = await CreateService(context).ExecuteAsync(
            new ApplicationActionInput
            {
                Id = scenario.RequestId,
                RowVersion = scenario.RowVersion,
                Reason = "Sinh viên không nhận phòng trong thời hạn giữ chỗ."
            },
            HousingRequestAction.Reject,
            scenario.ManagerId);

        Assert.True(result.Success);
        await using var verify = database.CreateContext();
        var request = await verify.HousingRequests
            .Include(x => x.ApprovedRoomHold)
            .Include(x => x.Events)
            .SingleAsync(x => x.Id == scenario.RequestId);
        Assert.Equal(HousingRequestStatus.Rejected, request.Status);
        Assert.Equal(Now, request.ApprovedRoomHold!.ReleasedAt);
        Assert.Contains(request.Events, x => x.Status == HousingRequestStatus.Rejected);
    }

    [SqlServerFact]
    public async Task Two_concurrent_confirmations_create_exactly_one_stay()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        AwaitingMoveInScenario scenario;
        await using (var setup = database.CreateContext())
            scenario = await CreateAwaitingMoveInAsync(setup, Now.AddHours(2));

        async Task<ApplicationManagementResult> ConfirmAsync()
        {
            await using var context = database.CreateContext();
            return await CreateService(context).ExecuteAsync(
                new ApplicationActionInput { Id = scenario.RequestId, RowVersion = scenario.RowVersion },
                HousingRequestAction.ConfirmMoveIn,
                scenario.ManagerId);
        }

        var results = await Task.WhenAll(ConfirmAsync(), ConfirmAsync());

        Assert.Single(results, x => x.Success);
        await using var verify = database.CreateContext();
        Assert.Equal(1, await verify.Stays.CountAsync(x => x.HousingRequestId == scenario.RequestId));
        var request = await verify.HousingRequests.Include(x => x.ApprovedRoomHold).SingleAsync(x => x.Id == scenario.RequestId);
        Assert.Equal(HousingRequestStatus.Approved, request.Status);
        Assert.NotNull(request.ApprovedRoomHold!.ReleasedAt);
    }

    [SqlServerFact]
    public async Task Two_approvals_for_the_last_bed_create_exactly_one_hold()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        ApprovalRaceScenario scenario;
        await using (var setup = database.CreateContext())
            scenario = await CreateApprovalRaceAsync(setup);

        async Task<ApplicationManagementResult> ApproveAsync(long id, string rowVersion)
        {
            await using var context = database.CreateContext();
            return await CreateService(context).ExecuteAsync(
                new ApplicationActionInput { Id = id, RowVersion = rowVersion },
                HousingRequestAction.Approve,
                scenario.ManagerId);
        }

        var results = await Task.WhenAll(
            ApproveAsync(scenario.FirstRequestId, scenario.FirstRowVersion),
            ApproveAsync(scenario.SecondRequestId, scenario.SecondRowVersion));

        Assert.Single(results, x => x.Success);
        await using var verify = database.CreateContext();
        Assert.Equal(1, await verify.RoomHolds.CountAsync(x => x.RoomId == scenario.RoomId && x.ReleasedAt == null));
        Assert.Equal(1, await verify.RoomHolds.Where(x => x.RoomId == scenario.RoomId && x.ReleasedAt == null).SumAsync(x => x.HeldBeds));
        Assert.Equal(1, await verify.HousingRequests.CountAsync(x => x.Status == HousingRequestStatus.AwaitingMoveIn));
        Assert.Equal(1, await verify.HousingRequests.CountAsync(x => x.Status == HousingRequestStatus.UnderReview));
    }

    private static ApplicationManagementService CreateService(DormitoryManagementSystem.Data.DormitoryDbContext context)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(Now));
        return new ApplicationManagementService(context, new BillingLedgerService(context, time), time);
    }

    private static async Task<AwaitingMoveInScenario> CreateAwaitingMoveInAsync(
        DormitoryManagementSystem.Data.DormitoryDbContext db,
        DateTime holdExpiresAt)
    {
        var manager = NewManager();
        var building = new Building { Code = "QA", Name = "Tòa QA", SiteName = "Cơ sở kiểm thử", SortOrder = 1 };
        var floor = new Floor { Number = 1, Building = building };
        var room = new Room { Code = "QA-101", Floor = floor, RoomType = "Nam", Capacity = 8, MonthlyPrice = 1_000_000m };
        var student = new StudentProfile
        {
            StudentCode = "SV-QA-001",
            FullName = "Sinh viên kiểm thử",
            Gender = "Nam",
            Faculty = "Kiểm thử",
            PhoneNumber = "0900000000"
        };
        var hold = new RoomHold { Room = room, HeldBeds = 1, ExpiresAt = holdExpiresAt };
        var request = new HousingRequest
        {
            Code = "DK-2026-QA01",
            StudentProfile = student,
            TargetRoom = room,
            ApprovedRoomHold = hold,
            Type = HousingRequestType.Registration,
            Status = HousingRequestStatus.AwaitingMoveIn,
            StudentReason = "Đăng ký kiểm thử",
            SubmittedAt = Now.AddDays(-1),
            ActionRequiredAt = Now.AddHours(-1),
            ReviewedAt = Now.AddHours(-1)
        };
        db.AddRange(manager, request);
        await db.SaveChangesAsync();
        return new(request.Id, Convert.ToBase64String(request.RowVersion), manager.Id);
    }

    private static async Task<ApprovalRaceScenario> CreateApprovalRaceAsync(
        DormitoryManagementSystem.Data.DormitoryDbContext db)
    {
        var manager = NewManager();
        var building = new Building { Code = "QC", Name = "Tòa QC", SiteName = "Cơ sở kiểm thử", SortOrder = 1 };
        var floor = new Floor { Number = 1, Building = building };
        var room = new Room { Code = "QC-101", Floor = floor, RoomType = "Nam", Capacity = 1, MonthlyPrice = 1_000_000m };
        HousingRequest NewRequest(string suffix) => new()
        {
            Code = $"DK-2026-QC{suffix}",
            StudentProfile = new StudentProfile
            {
                StudentCode = $"SV-QC-{suffix}",
                FullName = $"Sinh viên {suffix}",
                Gender = "Nam",
                Faculty = "Kiểm thử",
                PhoneNumber = $"09000000{suffix}"
            },
            TargetRoom = room,
            Type = HousingRequestType.Registration,
            Status = HousingRequestStatus.UnderReview,
            StudentReason = "Đăng ký giường cuối",
            SubmittedAt = Now.AddDays(-1),
            ActionRequiredAt = Now.AddHours(-1),
            ReviewedAt = Now.AddHours(-1)
        };
        var first = NewRequest("01");
        var second = NewRequest("02");
        db.AddRange(manager, first, second);
        await db.SaveChangesAsync();
        return new(
            room.Id,
            first.Id,
            Convert.ToBase64String(first.RowVersion),
            second.Id,
            Convert.ToBase64String(second.RowVersion),
            manager.Id);
    }

    private static ApplicationUser NewManager()
    {
        var id = Guid.NewGuid().ToString("N");
        return new ApplicationUser
        {
            Id = $"manager-{id}",
            UserName = $"manager-{id}@test.local",
            NormalizedUserName = $"MANAGER-{id}@TEST.LOCAL".ToUpperInvariant(),
            Email = $"manager-{id}@test.local",
            NormalizedEmail = $"MANAGER-{id}@TEST.LOCAL".ToUpperInvariant(),
            FullName = "Quản lý kiểm thử",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
    }

    private sealed record AwaitingMoveInScenario(long RequestId, string RowVersion, string ManagerId);
    private sealed record ApprovalRaceScenario(
        int RoomId,
        long FirstRequestId,
        string FirstRowVersion,
        long SecondRequestId,
        string SecondRowVersion,
        string ManagerId);
}
