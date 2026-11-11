using DormitoryManagementSystem.Services;
using Xunit;

namespace DormitoryManagementSystem.Tests;

public sealed class OperationsMetricsPolicyTests
{
    [Theory]
    [InlineData(1142, 1280, 89.2)]
    [InlineData(0, 1280, 0)]
    [InlineData(-2, 8, 0)]
    [InlineData(9, 8, 100)]
    [InlineData(4, 0, 0)]
    public void Percentage_is_rounded_and_clamped(int part, int whole, decimal expected)
    {
        Assert.Equal(expected, OperationsMetricsPolicy.Percentage(part, whole));
    }
}
