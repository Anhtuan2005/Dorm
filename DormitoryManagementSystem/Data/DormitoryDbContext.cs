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
    public DbSet<RoomPhoto> RoomPhotos => Set<RoomPhoto>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<Stay> Stays => Set<Stay>();
    public DbSet<RoomHold> RoomHolds => Set<RoomHold>();
    public DbSet<HousingRequest> HousingRequests => Set<HousingRequest>();
    public DbSet<HousingRequestEvent> HousingRequestEvents => Set<HousingRequestEvent>();
    public DbSet<BillingPeriod> BillingPeriods => Set<BillingPeriod>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<VnpayCheckout> VnpayCheckouts => Set<VnpayCheckout>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<MaintenanceFollower> MaintenanceFollowers => Set<MaintenanceFollower>();
    public DbSet<MaintenanceUpdate> MaintenanceUpdates => Set<MaintenanceUpdate>();
    public DbSet<MaintenanceAttachment> MaintenanceAttachments => Set<MaintenanceAttachment>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<ResidenceTerm> ResidenceTerms => Set<ResidenceTerm>();
    public DbSet<StayRenewal> StayRenewals => Set<StayRenewal>();
    public DbSet<RoomMeterReading> RoomMeterReadings => Set<RoomMeterReading>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<RoomMeterReading>().HasKey(x => new { x.BillingPeriodId, x.RoomId });
        builder.Entity<RoomMeterReading>().HasOne(x => x.Room).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.Entity<StayRenewal>().HasIndex(x => new { x.StayId, x.ResidenceTermId }).IsUnique();
        builder.Entity<StayRenewal>().HasIndex(x => x.StayId).HasFilter("[Approved] IS NULL").IsUnique();
        builder.Entity<ResidenceTerm>().HasIndex(x => x.Name).IsUnique();
        builder.HasSequence<long>("RepairNumbers").StartsAt(1);

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
        {
            table.HasCheckConstraint("CK_Stays_Chronology", "[EndedAt] IS NULL OR [EndedAt] >= [StartedAt]");
            table.HasCheckConstraint("CK_Stays_BedNumber_Positive", "[BedNumber] IS NULL OR [BedNumber] > 0");
        });
        builder.Entity<Stay>().HasIndex(x => new { x.RoomId, x.BedNumber }).IsUnique()
            .HasFilter("[EndedAt] IS NULL AND [BedNumber] IS NOT NULL");
        builder.Entity<Stay>().HasIndex(x => x.RoomId);
        builder.Entity<RoomHold>().ToTable(table => table.HasCheckConstraint(
            "CK_RoomHolds_NumberedBed", "[BedNumber] IS NULL OR ([BedNumber] > 0 AND [HeldBeds] = 1)"));
        builder.Entity<HousingRequest>().ToTable(table => table.HasCheckConstraint(
            "CK_HousingRequests_TargetBed", "[TargetBedNumber] IS NULL OR ([TargetBedNumber] > 0 AND [TargetRoomId] IS NOT NULL)"));
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
        builder.Entity<MaintenanceFollower>(entity =>
        {
            entity.HasKey(x => new { x.MaintenanceRequestId, x.StayId });
            entity.HasOne(x => x.MaintenanceRequest).WithMany(x => x.Followers)
                .HasForeignKey(x => x.MaintenanceRequestId);
            entity.HasOne(x => x.Stay).WithMany()
                .HasForeignKey(x => x.StayId).OnDelete(DeleteBehavior.Restrict);
        });
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

        builder.Entity<VnpayCheckout>(entity =>
        {
            entity.HasKey(x => x.Reference);
            entity.Property(x => x.Reference).IsUnicode(false);
            entity.Property(x => x.Amount).HasColumnType("decimal(12,0)");
            entity.Property(x => x.State).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
            entity.HasIndex(x => new { x.InvoiceId, x.State, x.ExpiresAtUtc });
            entity.HasIndex(x => new { x.MerchantCode, x.ProviderTransactionNumber }).IsUnique()
                .HasFilter("[ProviderTransactionNumber] IS NOT NULL");
            entity.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_VnpayCheckouts_Amount", "[Amount] BETWEEN 1 AND 9999999999");
                table.HasCheckConstraint("CK_VnpayCheckouts_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
            });
        });

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
