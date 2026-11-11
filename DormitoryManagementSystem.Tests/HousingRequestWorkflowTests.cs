using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using Xunit;

namespace DormitoryManagementSystem.Tests;

public sealed class HousingRequestWorkflowTests
{
    [Theory]
    [InlineData(HousingRequestStatus.Submitted, HousingRequestAction.BeginReview, HousingRequestStatus.UnderReview)]
    [InlineData(HousingRequestStatus.Submitted, HousingRequestAction.RequestInformation, HousingRequestStatus.NeedsInformation)]
    [InlineData(HousingRequestStatus.UnderReview, HousingRequestAction.RequestInformation, HousingRequestStatus.NeedsInformation)]
    [InlineData(HousingRequestStatus.NeedsInformation, HousingRequestAction.SubmitInformation, HousingRequestStatus.Submitted)]
    [InlineData(HousingRequestStatus.UnderReview, HousingRequestAction.Approve, HousingRequestStatus.AwaitingMoveIn)]
    [InlineData(HousingRequestStatus.AwaitingMoveIn, HousingRequestAction.ConfirmMoveIn, HousingRequestStatus.Approved)]
    [InlineData(HousingRequestStatus.NeedsInformation, HousingRequestAction.Reject, HousingRequestStatus.Rejected)]
    [InlineData(HousingRequestStatus.AwaitingMoveIn, HousingRequestAction.Reject, HousingRequestStatus.Rejected)]
    [InlineData(HousingRequestStatus.UnderReview, HousingRequestAction.Cancel, HousingRequestStatus.Cancelled)]
    public void Allowed_transitions_have_one_explicit_destination(
        HousingRequestStatus current,
        HousingRequestAction action,
        HousingRequestStatus expected)
    {
        var result = HousingRequestWorkflow.Transition(current, action);

        Assert.True(result.Allowed);
        Assert.Equal(expected, result.NextStatus);
    }

    [Theory]
    [InlineData(HousingRequestStatus.Submitted, HousingRequestAction.Approve)]
    [InlineData(HousingRequestStatus.NeedsInformation, HousingRequestAction.BeginReview)]
    [InlineData(HousingRequestStatus.Approved, HousingRequestAction.Cancel)]
    [InlineData(HousingRequestStatus.Rejected, HousingRequestAction.RequestInformation)]
    [InlineData(HousingRequestStatus.UnderReview, HousingRequestAction.ConfirmMoveIn)]
    public void Invalid_transitions_are_rejected_without_changing_state(
        HousingRequestStatus current,
        HousingRequestAction action)
    {
        var result = HousingRequestWorkflow.Transition(current, action);

        Assert.False(result.Allowed);
        Assert.Equal(current, result.NextStatus);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public void Awaiting_move_in_stays_active_and_returns_to_management()
    {
        var now = new DateTime(2026, 9, 10, 2, 0, 0, DateTimeKind.Utc);

        var result = HousingRequestWorkflow.Describe(HousingRequestStatus.AwaitingMoveIn, now.AddHours(-2), now);

        Assert.True(result.IsActive);
        Assert.Equal(HousingRequestResponsibility.Management, result.Responsibility);
        Assert.Equal("Chờ xác nhận nhận phòng", result.NextActionLabel);
    }

    [Fact]
    public void Checkout_approval_completes_without_a_move_in_handoff()
    {
        var result = HousingRequestWorkflow.Transition(
            HousingRequestStatus.UnderReview,
            HousingRequestAction.Approve,
            HousingRequestType.Checkout);

        Assert.True(result.Allowed);
        Assert.Equal(HousingRequestStatus.Approved, result.NextStatus);
    }

    [Fact]
    public void Manager_work_becomes_overdue_at_the_24_hour_boundary()
    {
        var now = new DateTime(2026, 9, 10, 2, 0, 0, DateTimeKind.Utc);

        var before = HousingRequestWorkflow.Describe(HousingRequestStatus.Submitted, now.AddHours(-23), now);
        var atBoundary = HousingRequestWorkflow.Describe(HousingRequestStatus.Submitted, now.AddHours(-24), now);

        Assert.False(before.IsManagerOverdue);
        Assert.True(atBoundary.IsManagerOverdue);
        Assert.Equal(HousingRequestResponsibility.Management, atBoundary.Responsibility);
    }

    [Fact]
    public void Waiting_for_student_never_counts_as_manager_sla_breach()
    {
        var now = new DateTime(2026, 9, 10, 2, 0, 0, DateTimeKind.Utc);

        var result = HousingRequestWorkflow.Describe(HousingRequestStatus.NeedsInformation, now.AddDays(-5), now);

        Assert.False(result.IsManagerOverdue);
        Assert.Equal(HousingRequestResponsibility.Student, result.Responsibility);
        Assert.Equal(120, result.WaitingHours);
    }
}
