using DormitoryManagementSystem.IntegrationTests;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using DormitoryManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class InvoiceIssuanceRegressionTests
{
    private static IssueInvoicesInput Period(string code, string? building = null) => new() {
        PeriodCode = code, PeriodName = code, StartDate = new(2026,11,1),
        EndDate = new(2026,11,30), DueDate = new(2026,11,20), BuildingCode = building
    };
    private static Room MakeRoom(string code, decimal rate) => new() {
        Code = code + "-101", MonthlyPrice = rate, Capacity = 8, RoomType = "Nam",
        Floor = new() { Number = 1, Building = new() { Code = code, Name = code, SiteName = "Fixture" } }
    };
    [SqlServerFact]
    public async Task Building_batches_and_full_issue_bill_the_same_stay_using_Vietnam_period_boundaries()
    {
        await using var fixture = await SqlServerTestDatabase.CreateAsync();
        await using var db = fixture.CreateContext();
        var a = MakeRoom("A",1100000m);
        var c = MakeRoom("C",1350000m);
        var transfer = new StudentProfile { StudentCode = "ST1", FullName = "Transfer", Gender = "Nam" };
        var nextMonth = new StudentProfile { StudentCode = "ST2", FullName = "Next month", Gender = "Nam" };
        var move = new DateTime(2026,11,15,8,0,0,DateTimeKind.Utc);
        db.Stays.AddRange(
            new Stay { StudentProfile = transfer, Room = a, StartedAt = move.AddDays(-20), EndedAt = move },
            new Stay { StudentProfile = transfer, Room = c, StartedAt = move },
            new Stay { StudentProfile = nextMonth, Room = c, StartedAt = new DateTime(2026,11,30,20,0,0,DateTimeKind.Utc) }
        );
        await db.SaveChangesAsync();
        var service = new BillingLedgerService(db, TimeProvider.System);
        Assert.True((await service.IssueInvoicesAsync(Period("SEPARATE", "A"))).Success);
        Assert.True((await service.IssueInvoicesAsync(Period("SEPARATE", "C"))).Success);
        Assert.True((await service.IssueInvoicesAsync(Period("ALL"))).Success);
        var invoices = await db.Invoices.Select(x => new { Student = x.StudentProfile.StudentCode,
            Period = x.BillingPeriod.Code, Room = x.Stay!.Room.Code, x.TotalAmount }).ToListAsync();
        var transferRows = invoices.Where(x => x.Student == "ST1").ToList();
        Assert.Equal(1350000m,transferRows.Single(x => x.Period == "SEPARATE").TotalAmount);
        Assert.Equal(1350000m,transferRows.Single(x => x.Period == "ALL").TotalAmount);
        Assert.DoesNotContain(invoices, x => x.Student == "ST2");
        Assert.Equal(2, invoices.Count);
    }
}

