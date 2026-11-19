using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Models;

public sealed class ResidenceTerm
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    public DateOnly Opens { get; set; }
    public DateOnly Closes { get; set; }
    public DateOnly Starts { get; set; }
    public DateOnly Ends { get; set; }
}

public sealed class StayRenewal
{
    public int Id { get; set; }
    public int StayId { get; set; }
    public Stay Stay { get; set; } = null!;
    public int ResidenceTermId { get; set; }
    public ResidenceTerm ResidenceTerm { get; set; } = null!;
    public DateTime RequestedUtc { get; set; }
    public bool? Approved { get; set; }
    public DateTime? DecidedUtc { get; set; }
    [MaxLength(450)] public string? DecidedById { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}

public sealed class RoomMeterReading
{
    public int BillingPeriodId { get; set; }
    public BillingPeriod BillingPeriod { get; set; } = null!;
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    [Precision(12, 2)] public decimal PowerStart { get; set; }
    [Precision(12, 2)] public decimal PowerEnd { get; set; }
    [Precision(12, 2)] public decimal PowerRate { get; set; }
    [Precision(12, 2)] public decimal WaterStart { get; set; }
    [Precision(12, 2)] public decimal WaterEnd { get; set; }
    [Precision(12, 2)] public decimal WaterRate { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}
