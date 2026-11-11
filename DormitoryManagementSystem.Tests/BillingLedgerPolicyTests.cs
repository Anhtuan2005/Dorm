using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using Xunit;

namespace DormitoryManagementSystem.Tests;

public sealed class BillingLedgerPolicyTests
{
    private static readonly DateOnly BusinessDate = new(2026, 9, 10);

    [Fact]
    public void Unpaid_invoice_due_today_is_awaiting()
    {
        var result = Summarize(1_500_000m, [], BusinessDate);

        Assert.Equal(BillingPaymentState.Awaiting, result.State);
        Assert.Equal(1_500_000m, result.Balance);
        Assert.True(result.CanCollect);
    }

    [Fact]
    public void Partially_collected_invoice_is_partial()
    {
        var result = Summarize(1_500_000m, [600_000m], BusinessDate.AddDays(2));

        Assert.Equal(BillingPaymentState.Partial, result.State);
        Assert.Equal(600_000m, result.Collected);
        Assert.Equal(900_000m, result.Balance);
    }

    [Fact]
    public void Fully_collected_invoice_is_paid_and_closed_for_collection()
    {
        var result = Summarize(1_500_000m, [500_000m, 1_000_000m], BusinessDate.AddDays(-1));

        Assert.Equal(BillingPaymentState.Paid, result.State);
        Assert.Equal(0m, result.Balance);
        Assert.False(result.CanCollect);
    }

    [Fact]
    public void Unpaid_invoice_becomes_overdue_only_after_due_date()
    {
        var dueToday = Summarize(1_500_000m, [], BusinessDate);
        var dueYesterday = Summarize(1_500_000m, [], BusinessDate.AddDays(-1));

        Assert.Equal(BillingPaymentState.Awaiting, dueToday.State);
        Assert.Equal(BillingPaymentState.Overdue, dueYesterday.State);
    }

    [Fact]
    public void Reversal_offsets_the_original_receipt()
    {
        var result = Summarize(1_500_000m, [500_000m, -500_000m], BusinessDate.AddDays(2));

        Assert.Equal(BillingPaymentState.Awaiting, result.State);
        Assert.Equal(0m, result.Collected);
        Assert.Equal(1_500_000m, result.Balance);
    }

    [Fact]
    public void Cancelled_invoice_cannot_collect_even_with_a_balance()
    {
        var result = BillingLedgerPolicy.Summarize(
            1_500_000m,
            [500_000m],
            BusinessDate.AddDays(-2),
            BusinessDate,
            InvoiceStatus.Cancelled);

        Assert.Equal(BillingPaymentState.Cancelled, result.State);
        Assert.Equal(1_000_000m, result.Balance);
        Assert.False(result.CanCollect);
    }

    private static BillingLedgerSummary Summarize(
        decimal total,
        IEnumerable<decimal> transactions,
        DateOnly dueDate) =>
        BillingLedgerPolicy.Summarize(total, transactions, dueDate, BusinessDate, InvoiceStatus.Issued);
}

public sealed class RoomCapacityPolicyTests
{
    [Fact]
    public void Capacity_is_shared_between_occupied_held_blocked_and_assignable()
    {
        var result = RoomCapacityPolicy.Calculate(capacity: 8, occupied: 4, held: 1, blocked: 2);

        Assert.Equal(8, result.Capacity);
        Assert.Equal(4, result.Occupied);
        Assert.Equal(1, result.Held);
        Assert.Equal(2, result.Blocked);
        Assert.Equal(1, result.Assignable);
        Assert.True(result.CanAssign);
    }

    [Fact]
    public void Blocked_and_held_counts_are_clamped_after_occupancy()
    {
        var result = RoomCapacityPolicy.Calculate(capacity: 8, occupied: 7, held: 4, blocked: 3);

        Assert.Equal(1, result.Blocked);
        Assert.Equal(0, result.Held);
        Assert.Equal(0, result.Assignable);
        Assert.Equal("1 chỗ tạm khóa do sự cố", result.AvailabilityReason);
    }

    [Fact]
    public void Maintenance_block_cannot_consume_an_active_hold()
    {
        var now = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);
        var room = RoomWithCapacity(8);
        for (var index = 0; index < 6; index++) room.Stays.Add(new Stay());
        room.Holds.Add(new RoomHold { Id = 10, RoomId = room.Id, HeldBeds = 1, ExpiresAt = now.AddHours(2) });
        var request = new MaintenanceRequest { Id = 20, RoomId = room.Id, Status = MaintenanceStatus.InProgress };
        room.MaintenanceRequests.Add(request);

        var decision = RoomCapacityPolicy.CanSetBlockedBeds(room, request.Id, 2, now);

        Assert.False(decision.Allowed);
        Assert.Equal(1, decision.MaximumBlockedBeds);
    }

    [Theory]
    [InlineData(true, false, 1, 1, "đã được giải phóng")]
    [InlineData(false, true, 1, 1, "đã hết hạn")]
    [InlineData(false, false, 2, 1, "không thuộc phòng đích")]
    [InlineData(false, false, 1, 2, "không giữ đúng 1 chỗ")]
    public void Move_in_rejects_an_invalid_hold(
        bool released,
        bool expired,
        int holdRoomId,
        int heldBeds,
        string expectedError)
    {
        var now = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);
        var room = RoomWithCapacity(8);
        var hold = new RoomHold
        {
            Id = 30,
            RoomId = holdRoomId,
            HeldBeds = heldBeds,
            ExpiresAt = expired ? now : now.AddHours(2),
            ReleasedAt = released ? now.AddMinutes(-1) : null
        };

        var decision = RoomCapacityPolicy.CanConsumeHold(room, hold, requiredBeds: 1, now);

        Assert.False(decision.Allowed);
        Assert.Contains(expectedError, decision.ErrorMessage);
    }

    [Fact]
    public void Move_in_consumes_only_the_capacity_reserved_by_its_hold()
    {
        var now = new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc);
        var room = RoomWithCapacity(8);
        for (var index = 0; index < 6; index++) room.Stays.Add(new Stay());
        room.Holds.Add(new RoomHold { Id = 31, RoomId = room.Id, HeldBeds = 1, ExpiresAt = now.AddHours(2) });
        var ownHold = new RoomHold { Id = 30, RoomId = room.Id, HeldBeds = 1, ExpiresAt = now.AddHours(2) };
        room.Holds.Add(ownHold);

        var decision = RoomCapacityPolicy.CanConsumeHold(room, ownHold, requiredBeds: 1, now);

        Assert.True(decision.Allowed);
    }

    private static Room RoomWithCapacity(int capacity) => new() { Id = 1, Code = "A-101", Capacity = capacity };
}
