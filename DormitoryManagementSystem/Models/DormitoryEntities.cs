using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models;

public sealed class Building
{
    public int Id { get; set; }
    [MaxLength(4)] public string Code { get; set; } = string.Empty;
    [MaxLength(80)] public string Name { get; set; } = string.Empty;
    [MaxLength(100)] public string SiteName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ICollection<Floor> Floors { get; set; } = [];
}

public sealed class Floor
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;
    public int Number { get; set; }
    public ICollection<Room> Rooms { get; set; } = [];
}

public sealed class Room
{
    public int Id { get; set; }
    public int FloorId { get; set; }
    public Floor Floor { get; set; } = null!;
    [MaxLength(12)] public string Code { get; set; } = string.Empty;
    [MaxLength(24)] public string RoomType { get; set; } = string.Empty;
    [MaxLength(240)] public string Amenities { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal MonthlyPrice { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ICollection<Stay> Stays { get; set; } = [];
    public ICollection<RoomHold> Holds { get; set; } = [];
    public ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = [];
}

public sealed class StudentProfile
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    [MaxLength(20)] public string StudentCode { get; set; } = string.Empty;
    [MaxLength(100)] public string FullName { get; set; } = string.Empty;
    [MaxLength(16)] public string Gender { get; set; } = string.Empty;
    [MaxLength(100)] public string Faculty { get; set; } = string.Empty;
    [MaxLength(20)] public string PhoneNumber { get; set; } = string.Empty;
    public ICollection<Stay> Stays { get; set; } = [];
    public ICollection<HousingRequest> HousingRequests { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
    public ICollection<MaintenanceRequest> ReportedMaintenanceRequests { get; set; } = [];
}

public sealed class Stay
{
    public int Id { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = null!;
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public sealed class RoomHold
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int HeldBeds { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
}

public enum HousingRequestType { Registration, Transfer, Checkout }
public enum HousingRequestStatus { Submitted, UnderReview, Approved, Rejected, Cancelled }

public sealed class HousingRequest
{
    public long Id { get; set; }
    [MaxLength(24)] public string Code { get; set; } = string.Empty;
    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = null!;
    public int? TargetRoomId { get; set; }
    public Room? TargetRoom { get; set; }
    public HousingRequestType Type { get; set; }
    public HousingRequestStatus Status { get; set; }
    [MaxLength(600)] public string StudentReason { get; set; } = string.Empty;
    [MaxLength(600)] public string? DecisionReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public sealed class BillingPeriod
{
    public int Id { get; set; }
    [MaxLength(12)] public string Code { get; set; } = string.Empty;
    [MaxLength(80)] public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly DueDate { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = [];
}

public enum InvoiceStatus { Issued, PartiallyPaid, Paid, Overdue, Cancelled }

public sealed class Invoice
{
    public long Id { get; set; }
    [MaxLength(24)] public string Code { get; set; } = string.Empty;
    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = null!;
    public int BillingPeriodId { get; set; }
    public BillingPeriod BillingPeriod { get; set; } = null!;
    public int? StayId { get; set; }
    public Stay? Stay { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTime IssuedAt { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public ICollection<InvoiceLine> Lines { get; set; } = [];
}

public sealed class InvoiceLine
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    [MaxLength(32)] public string ChargeType { get; set; } = string.Empty;
    [MaxLength(120)] public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public enum MaintenancePriority { Low, Medium, High, Urgent }
public enum MaintenanceStatus { New, Assigned, InProgress, WaitingParts, Resolved, Closed }

public sealed class MaintenanceRequest
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int? ReporterStudentId { get; set; }
    public StudentProfile? ReporterStudent { get; set; }
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(180)] public string Title { get; set; } = string.Empty;
    [MaxLength(60)] public string Category { get; set; } = string.Empty;
    [MaxLength(1000)] public string Description { get; set; } = string.Empty;
    public MaintenancePriority Priority { get; set; }
    public MaintenanceStatus Status { get; set; }
    public int BlockedBeds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<MaintenanceUpdate> Updates { get; set; } = [];
}

public sealed class MaintenanceUpdate
{
    public long Id { get; set; }
    public int MaintenanceRequestId { get; set; }
    public MaintenanceRequest MaintenanceRequest { get; set; } = null!;
    public MaintenanceStatus Status { get; set; }
    [MaxLength(600)] public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class Notification
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    [MaxLength(40)] public string Type { get; set; } = string.Empty;
    [MaxLength(160)] public string Title { get; set; } = string.Empty;
    [MaxLength(300)] public string Body { get; set; } = string.Empty;
    [MaxLength(240)] public string TargetUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
