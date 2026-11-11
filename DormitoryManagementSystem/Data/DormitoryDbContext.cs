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
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<MaintenanceUpdate> MaintenanceUpdates => Set<MaintenanceUpdate>();
    public DbSet<MaintenanceAttachment> MaintenanceAttachments => Set<MaintenanceAttachment>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Building>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<Floor>().HasIndex(x => new { x.BuildingId, x.Number }).IsUnique();
        builder.Entity<Room>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<Room>().ToTable(table =>
            table.HasCheckConstraint("CK_Rooms_Capacity_Positive", "[Capacity] > 0"));
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
        builder.Entity<Stay>().Property(x => x.RowVersion).IsRowVersion();
        builder.Entity<Stay>().ToTable(table =>
            table.HasCheckConstraint("CK_Stays_Chronology", "[EndedAt] IS NULL OR [EndedAt] >= [StartedAt]"));
        builder.Entity<Stay>()
            .HasIndex(x => x.HousingRequestId)
            .HasFilter("[HousingRequestId] IS NOT NULL")
            .IsUnique();
        builder.Entity<MaintenanceRequest>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<MaintenanceRequest>().ToTable(table =>
            table.HasCheckConstraint("CK_MaintenanceRequests_BlockedBeds_NonNegative", "[BlockedBeds] >= 0"));
        builder.Entity<MaintenanceRequest>().Property(x => x.Priority).HasConversion<string>();
        builder.Entity<MaintenanceRequest>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<MaintenanceRequest>()
            .HasOne(x => x.ReporterStudent)
            .WithMany(x => x.ReportedMaintenanceRequests)
            .HasForeignKey(x => x.ReporterStudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MaintenanceUpdate>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<MaintenanceUpdate>()
            .HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<MaintenanceAttachment>().HasIndex(x => x.StoredName).IsUnique();
        builder.Entity<MaintenanceAttachment>()
            .HasOne(x => x.MaintenanceRequest)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.MaintenanceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<HousingRequest>().HasIndex(x => x.Code).IsUnique();
        builder.HasSequence<long>("HousingRequestNumbers").StartsAt(1000).IncrementsBy(1);
        builder.Entity<HousingRequest>()
            .HasIndex(x => x.StudentProfileId)
            .IsUnique()
            .HasFilter("[Status] IN (N'Submitted', N'UnderReview', N'NeedsInformation', N'AwaitingMoveIn')");
        builder.Entity<HousingRequest>().Property(x => x.Type).HasConversion<string>();
        builder.Entity<HousingRequest>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<HousingRequest>()
            .HasOne(x => x.TargetRoom)
            .WithMany()
            .HasForeignKey(x => x.TargetRoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HousingRequest>()
            .HasOne(x => x.SourceStay)
            .WithMany()
            .HasForeignKey(x => x.SourceStayId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HousingRequest>()
            .HasOne(x => x.CreatedStay)
            .WithOne(x => x.HousingRequest)
            .HasForeignKey<Stay>(x => x.HousingRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HousingRequest>()
            .HasIndex(x => x.ApprovedRoomHoldId)
            .IsUnique()
            .HasFilter("[ApprovedRoomHoldId] IS NOT NULL");
        builder.Entity<HousingRequest>()
            .HasOne(x => x.ApprovedRoomHold)
            .WithMany()
            .HasForeignKey(x => x.ApprovedRoomHoldId)
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

        builder.Entity<RoomHold>().ToTable(table =>
            table.HasCheckConstraint("CK_RoomHolds_HeldBeds_Positive", "[HeldBeds] > 0"));

        builder.Entity<BillingPeriod>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<Invoice>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<Invoice>().HasIndex(x => new { x.StudentProfileId, x.BillingPeriodId }).IsUnique();
        builder.Entity<Invoice>().Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Entity<Invoice>().Property(x => x.Status).HasConversion<string>();
        builder.Entity<Invoice>()
            .HasOne(x => x.Stay)
            .WithMany()
            .HasForeignKey(x => x.StayId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<InvoiceLine>().Property(x => x.Amount).HasColumnType("decimal(18,2)");
        builder.Entity<PaymentTransaction>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<PaymentTransaction>().HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.Entity<PaymentTransaction>()
            .HasIndex(x => x.ReversesTransactionId)
            .IsUnique()
            .HasFilter("[ReversesTransactionId] IS NOT NULL");
        builder.Entity<PaymentTransaction>().Property(x => x.Amount).HasColumnType("decimal(18,2)");
        builder.Entity<PaymentTransaction>().Property(x => x.Method).HasConversion<string>();
        builder.Entity<PaymentTransaction>()
            .HasOne(x => x.RecordedByUser)
            .WithMany()
            .HasForeignKey(x => x.RecordedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<PaymentTransaction>()
            .HasOne(x => x.ReversesTransaction)
            .WithOne(x => x.ReversalTransaction)
            .HasForeignKey<PaymentTransaction>(x => x.ReversesTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasSequence<long>("ReceiptNumbers").StartsAt(1001).IncrementsBy(1);

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
