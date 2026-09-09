using DormitoryManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DormitoryManagementSystem.Data;

public sealed class DormitoryDbContext(DbContextOptions<DormitoryDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<Stay> Stays => Set<Stay>();
    public DbSet<RoomHold> RoomHolds => Set<RoomHold>();
    public DbSet<HousingRequest> HousingRequests => Set<HousingRequest>();
    public DbSet<HousingRequestEvent> HousingRequestEvents => Set<HousingRequestEvent>();
    public DbSet<BillingPeriod> BillingPeriods => Set<BillingPeriod>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<MaintenanceUpdate> MaintenanceUpdates => Set<MaintenanceUpdate>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Building>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<Floor>().HasIndex(x => new { x.BuildingId, x.Number }).IsUnique();
        builder.Entity<Room>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<Room>().Property(x => x.MonthlyPrice).HasColumnType("decimal(18,2)");
        builder.Entity<Room>().Property(x => x.RowVersion).IsRowVersion();
        builder.Entity<StudentProfile>().HasIndex(x => x.StudentCode).IsUnique();
        builder.Entity<StudentProfile>().HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        builder.Entity<StudentProfile>()
            .HasOne(x => x.User)
            .WithOne(x => x.StudentProfile)
            .HasForeignKey<StudentProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Stay>()
            .HasIndex(x => x.StudentProfileId)
            .HasFilter("[EndedAt] IS NULL")
            .IsUnique();
        builder.Entity<MaintenanceRequest>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<MaintenanceRequest>().Property(x => x.Priority).HasConversion<string>();
        builder.Entity<MaintenanceRequest>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<MaintenanceRequest>()
            .HasOne(x => x.ReporterStudent)
            .WithMany(x => x.ReportedMaintenanceRequests)
            .HasForeignKey(x => x.ReporterStudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MaintenanceUpdate>().Property(x => x.Status).HasConversion<string>();

        builder.Entity<HousingRequest>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<HousingRequest>()
            .HasIndex(x => x.StudentProfileId)
            .IsUnique()
            .HasFilter("[Status] IN (N'Submitted', N'UnderReview', N'NeedsInformation')");
        builder.Entity<HousingRequest>().Property(x => x.Type).HasConversion<string>();
        builder.Entity<HousingRequest>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<HousingRequest>()
            .HasOne(x => x.TargetRoom)
            .WithMany()
            .HasForeignKey(x => x.TargetRoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HousingRequest>()
            .HasOne(x => x.ReviewedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<HousingRequestEvent>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<HousingRequestEvent>()
            .HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<BillingPeriod>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<Invoice>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<Invoice>().HasIndex(x => new { x.StudentProfileId, x.BillingPeriodId }).IsUnique();
        builder.Entity<Invoice>().Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Entity<Invoice>().Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");
        builder.Entity<Invoice>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<Invoice>()
            .HasOne(x => x.Stay)
            .WithMany()
            .HasForeignKey(x => x.StayId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<InvoiceLine>().Property(x => x.Amount).HasColumnType("decimal(18,2)");

        builder.Entity<Notification>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Stay>()
            .HasOne(x => x.Room)
            .WithMany(x => x.Stays)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Stay>()
            .HasOne(x => x.StudentProfile)
            .WithMany(x => x.Stays)
            .HasForeignKey(x => x.StudentProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
