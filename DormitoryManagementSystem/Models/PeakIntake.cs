namespace DormitoryManagementSystem.Models;

public sealed class PeakIntake
{
    public int Id { get; set; }
    public DateOnly Starts { get; set; }
    public DateOnly Ends { get; set; }
    public int HoldHours { get; set; }
    public DateTime? ClosedUtc { get; set; }
}
