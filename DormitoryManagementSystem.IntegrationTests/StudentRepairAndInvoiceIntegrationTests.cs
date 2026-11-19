using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DormitoryManagementSystem.IntegrationTests;

public sealed class StudentRepairAndInvoiceIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);
    private const string Reporter = "repair-reporter";
    private const string Roommate = "repair-roommate";
    private const string Outsider = "repair-outsider";
    private const string Manager = "repair-manager";

    [SqlServerFact]
    public async Task Last_peak_bed_can_only_be_held_once_and_requires_approval_before_handover()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var floor = new Floor { Number = 1, Building = new() { Name = "KTX", Code = "QA", SiteName = "KTX" } };
        var room = new Room { Code = "QA-101", Floor = floor, Capacity = 1, RoomType = "Nam" };
        var applicants = Enumerable.Range(1, 24).Select(i => Profile($"peak-{i:00}")).ToList();
        foreach (var student in applicants) student.Gender = "Nam";
        db.StudentProfiles.AddRange(applicants);
        db.Users.Add(new() { Id = Manager, UserName = Manager });
        db.Rooms.Add(room);
        var today = DateOnly.FromDateTime(Now.UtcDateTime.AddHours(7));
        db.PeakIntakes.Add(new() { Starts = today, Ends = today, HoldHours = 12 });
        await db.SaveChangesAsync();

        async Task<StudentOperationResult> Apply(string account)
        {
            await using var scoped = database.CreateContext();
            return await Portal(scoped).SubmitHousingRequestAsync(account, new() {
                TargetRoomId = room.Id, TargetBedNumber = 1, Reason = "Đăng ký nội trú đầu năm học."
            });
        }

        var attempts = await Task.WhenAll(applicants.Select(x => Apply(x.User!.Id)));
        Assert.Single(attempts, x => x.Success);
        var request = await db.HousingRequests.AsNoTracking().SingleAsync();
        Assert.Equal(HousingRequestStatus.Submitted, request.Status);
        var hold = await db.RoomHolds.AsNoTracking().SingleAsync();
        Assert.Equal(hold.Id, request.ApprovedRoomHoldId);
        Assert.Equal(Now.AddHours(12).UtcDateTime, hold.ExpiresAt);
        Assert.Empty(await db.Stays.ToListAsync());

        var clock = new FixedTimeProvider(Now);
        var service = new ApplicationManagementService(db, new BillingLedgerService(db, clock), clock);
        async Task Act(HousingRequestAction action)
        {
            var version = await db.HousingRequests.Where(x => x.Id == request.Id).Select(x => x.RowVersion).SingleAsync();
            var result = await service.ExecuteAsync(new() { Id = request.Id, RowVersion = Convert.ToBase64String(version) }, action, Manager);
            Assert.True(result.Success, result.Message);
        }

        await Act(HousingRequestAction.BeginReview);
        var review = (await service.GetQueueAsync(new() { SelectedId = request.Id })).SelectedCase!;
        Assert.True(review.HasProvisionalHold);
        Assert.True(review.CanApprove);
        await Act(HousingRequestAction.Approve);
        var approved = await db.HousingRequests.AsNoTracking().SingleAsync();
        Assert.Equal(HousingRequestStatus.AwaitingMoveIn, approved.Status);
        Assert.Equal(hold.Id, approved.ApprovedRoomHoldId);
        Assert.Equal(Now.AddDays(7).UtcDateTime, (await db.RoomHolds.AsNoTracking().SingleAsync()).ExpiresAt);
        Assert.Empty(await db.Stays.ToListAsync());
    }

    [SqlServerFact]
    public async Task My_profile_uses_the_linked_account_and_displays_only_the_active_stay()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var floor = new Floor { Number = 1, Building = new() { Code = "QA", Name = "Tòa QA", SiteName = "KTX" } };
        var mine = Profile(Reporter, new Room { Code = "QA-101", Floor = floor, Capacity = 8 });
        mine.User!.Email = "resident@example.test";
        mine.PhoneNumber = "0900000000";
        mine.Stays.Single().BedNumber = 3;
        mine.Stays.Add(new() { Room = new() { Code = "QA-102", Floor = floor, Capacity = 8 },
            StartedAt = Now.AddMonths(-2).UtcDateTime, EndedAt = Now.AddMonths(-1).UtcDateTime });
        db.StudentProfiles.AddRange(mine, Profile(Outsider));
        await db.SaveChangesAsync();

        var portal = Portal(db);
        var profile = Assert.IsType<MyStudentProfileViewModel>(await portal.GetMyProfileAsync(Reporter));
        Assert.Equal("resident@example.test", profile.Student.Email);
        Assert.Equal(mine.PhoneNumber, profile.Student.PhoneNumber);
        Assert.Equal("QA-101", profile.CurrentStay?.RoomCode);
        Assert.Equal(3, profile.CurrentStay?.BedNumber);
        var other = Assert.IsType<MyStudentProfileViewModel>(await portal.GetMyProfileAsync(Outsider));
        Assert.Equal(Outsider, other.Student.StudentCode);
        Assert.Null(other.CurrentStay);
        Assert.Null(await portal.GetMyProfileAsync("unlinked-account"));
    }

    [SqlServerFact]
    public async Task Shared_repair_following_is_limited_to_current_roommates_and_sends_one_update_per_follower()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var floor = new Floor { Number = 1, Building = new() { Code = "QA", Name = "Tòa QA", SiteName = "KTX" } };
        var room = new Room { Code = "QA-101", Floor = floor, Capacity = 8 };
        var otherRoom = new Room { Code = "QA-102", Floor = floor, Capacity = 8 };
        var roommate = Profile(Roommate, room);
        context.StudentProfiles.AddRange(Profile(Reporter, room), roommate, Profile(Outsider, otherRoom));
        context.Users.Add(new() { Id = Manager, UserName = Manager });
        await context.SaveChangesAsync();

        var portal = Portal(context);
        Assert.True((await portal.CreateRepairAsync(Reporter, Request("Rò nước trong phòng", shared: true))).Success);
        Assert.True((await portal.CreateRepairAsync(Reporter, Request("Yêu cầu cá nhân", shared: false))).Success);
        var request = await context.MaintenanceRequests.SingleAsync(x => x.ShareWithRoom);
        var privateId = await context.MaintenanceRequests.Where(x => !x.ShareWithRoom).Select(x => x.Id).SingleAsync();

        Assert.Equal(request.Id, Assert.Single((await portal.GetRepairsAsync(Roommate)).SharedRepairs).Id);
        Assert.Empty((await portal.GetRepairsAsync(Outsider)).SharedRepairs);
        Assert.Empty((await portal.GetRepairsAsync(Reporter)).SharedRepairs);
        Assert.False((await portal.SetRepairFollowingAsync(Roommate, privateId, true)).Success);
        Assert.False((await portal.SetRepairFollowingAsync(Outsider, request.Id, true)).Success);

        async Task<StudentOperationResult> Follow()
        {
            await using var otherContext = database.CreateContext();
            return await Portal(otherContext).SetRepairFollowingAsync(Roommate, request.Id, true);
        }
        var results = await Task.WhenAll(Follow(), Follow());
        Assert.All(results, result => Assert.True(result.Success));
        Assert.Equal(1, await context.MaintenanceFollowers.CountAsync());
        Assert.True(Assert.Single((await portal.GetRepairsAsync(Roommate)).SharedRepairs).IsFollowing);

        await UpdateRepair(MaintenanceStatus.Assigned);
        var notice = await context.Notifications.SingleAsync(x => x.UserId == Roommate);
        Assert.EndsWith($"#repair-{request.Id}", notice.TargetUrl);
        Assert.DoesNotContain("Ghi chú của quản lý", notice.Body);
        Assert.Empty(await context.Notifications.Where(x => x.UserId == Outsider).ToListAsync());

        Assert.True((await portal.SetRepairFollowingAsync(Roommate, request.Id, false)).Success);
        await UpdateRepair(MaintenanceStatus.InProgress);
        Assert.Equal(1, await context.Notifications.CountAsync(x => x.UserId == Roommate));

        Assert.True((await portal.SetRepairFollowingAsync(Roommate, request.Id, true)).Success);
        roommate.Stays.Single().EndedAt = Now.UtcDateTime;
        await context.SaveChangesAsync();
        await UpdateRepair(MaintenanceStatus.Resolved);
        Assert.Equal(1, await context.Notifications.CountAsync(x => x.UserId == Roommate));
        Assert.Empty((await portal.GetRepairsAsync(Roommate)).SharedRepairs);

        async Task UpdateRepair(MaintenanceStatus status)
        {
            await using var managerContext = database.CreateContext();
            var version = await managerContext.MaintenanceRequests.Where(x => x.Id == request.Id)
                .Select(x => x.RowVersion).SingleAsync();
            var result = await new MaintenanceManagementService(managerContext, new FixedTimeProvider(Now))
                .UpdateAsync(new()
                {
                    RequestId = request.Id,
                    Status = status.ToString(),
                    Priority = "Medium",
                    AssignedTeam = "Tổ Điện nước",
                    Note = "Ghi chú của quản lý về việc xử lý.",
                    RowVersion = Convert.ToBase64String(version)
                }, Manager);
            Assert.True(result.Success, result.Message);
        }
    }

    [SqlServerFact]
    public async Task Invoice_comparison_skips_cancelled_periods_and_matches_legacy_charge_types()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var student = Profile(Reporter);
        var previous = InvoiceFor(8, InvoiceStatus.Issued, ("Electricity", 100_000), ("Water", 50_000), ("Room", 1_000_000));
        var cancelled = InvoiceFor(9, InvoiceStatus.Cancelled, ("Điện", 10_000));
        var current = InvoiceFor(10, InvoiceStatus.Issued, ("Điện", 95_000), ("Điện", 47_000), ("Tiền phòng", 1_000_000));
        context.Invoices.AddRange(previous, cancelled, current);
        await context.SaveChangesAsync();

        var invoices = (await Portal(context).GetInvoicesAsync(Reporter)).Invoices;
        var comparison = Assert.IsType<StudentInvoiceComparison>(invoices.Single(x => x.Id == current.Id).Comparison);
        Assert.Equal(previous.Id, comparison.PreviousInvoiceId);
        Assert.Equal(-8_000m, comparison.Difference);
        Assert.Equal(42_000m, comparison.Changes.Single(x => x.Label == "Tiền điện").Difference);
        Assert.Equal(-50_000m, comparison.Changes.Single(x => x.Label == "Tiền nước").Difference);
        Assert.DoesNotContain(comparison.Changes, x => x.Label == "Tiền phòng");
        Assert.Null(invoices.Single(x => x.Id == cancelled.Id).Comparison);
        Assert.Null(invoices.Single(x => x.Id == previous.Id).Comparison);

        Invoice InvoiceFor(int month, InvoiceStatus status, params (string Type, decimal Amount)[] charges)
        {
            var firstDay = new DateOnly(2026, month, 1);
            return new()
            {
                Code = $"QA-HD-{month}", StudentProfile = student, Status = status, IssuedAt = Now.UtcDateTime,
                BillingPeriod = new() { Code = $"QA-{month}", Name = $"Tháng {month}", StartDate = firstDay, EndDate = firstDay.AddMonths(1).AddDays(-1), DueDate = firstDay.AddDays(14) },
                TotalAmount = charges.Sum(x => x.Amount),
                Lines = charges.Select(x => new InvoiceLine { ChargeType = x.Type, Description = $"Chi phí {x.Type}", Amount = x.Amount }).ToList()
            };
        }
    }

    [SqlServerFact]
    public async Task Two_residents_submitting_repairs_receive_distinct_codes()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var room = new Room { Code = "QA-101", Capacity = 4, Floor = new Floor {
            Number = 1, Building = new Building { Code = "QA", Name = "Tòa QA", SiteName = "KTX" }
        } };
        db.StudentProfiles.AddRange(Profile(Reporter, room), Profile(Roommate, room));
        await db.SaveChangesAsync();

        async Task<StudentOperationResult> Send(string account)
        {
            await using var scoped = database.CreateContext();
            return await Portal(scoped).CreateRepairAsync(account, Request("Yêu cầu của " + account, false));
        }

        var results = await Task.WhenAll(Send(Reporter), Send(Roommate));
        Assert.All(results, result => Assert.True(result.Success, result.Message));
        Assert.Equal(2, await db.MaintenanceRequests.Select(x => x.Code).Distinct().CountAsync());
    }

    private static StudentPortalService Portal(DormitoryDbContext context) =>
        new(context, new BillingLedgerService(context, new FixedTimeProvider(Now)), new EmptyAttachments(), new FixedTimeProvider(Now));

    private static StudentProfile Profile(string account, Room? room = null) => new()
    {
        StudentCode = account, FullName = account, User = new() { Id = account, UserName = account, FullName = account },
        Stays = room is null ? [] : [new() { Room = room, StartedAt = Now.AddDays(-3).UtcDateTime }]
    };

    private static CreateRepairRequestInput Request(string title, bool shared) => new()
    {
        Title = title, ShareWithRoom = shared, Category = "Điện nước", Description = "Mô tả riêng của người gửi."
    };

    private sealed class EmptyAttachments : IMaintenanceAttachmentStorage
    {
        public Task<AttachmentPreparationResult> PrepareAsync(IReadOnlyList<IFormFile> files, DateTime createdAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AttachmentPreparationResult(true, string.Empty, []));
        public Task DeleteAsync(IEnumerable<MaintenanceAttachment> attachments) => Task.CompletedTask;
        public Task<AttachmentDownload?> OpenAsync(long attachmentId, string userId, bool canViewAll, CancellationToken cancellationToken = default) =>
            Task.FromResult<AttachmentDownload?>(null);
    }
}
