namespace DormitoryManagementSystem.Services;

public static class OperationsMetricsPolicy
{
    public static decimal Percentage(decimal part, decimal whole) => whole <= 0
        ? 0
        : Math.Round(Math.Clamp(part / whole * 100m, 0m, 100m), 1, MidpointRounding.AwayFromZero);
}
