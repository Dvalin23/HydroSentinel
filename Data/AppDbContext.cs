using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace HydroSentinel.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<DeviceEvent> DeviceEvents => Set<DeviceEvent>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();
    public DbSet<RecurringBillingAgreement> RecurringBillingAgreements => Set<RecurringBillingAgreement>();
    public DbSet<RecurringBillingTransaction> RecurringBillingTransactions => Set<RecurringBillingTransaction>();
    public DbSet<CustomerNotification> CustomerNotifications => Set<CustomerNotification>();

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        // SQLite cannot compare/order DateTimeOffset in SQL. Store as UTC ticks everywhere so the same
        // queries work on SQLite (dev) and PostgreSQL (prod). Applies to nullable properties too.
        cb.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    private sealed class UtcTicksConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long>
    {
        public UtcTicksConverter() : base(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)) { }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Device>(e =>
        {
            e.HasIndex(x => x.Serial).IsUnique();
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.Serial).HasMaxLength(64);
            e.Property(x => x.KeyHash).HasMaxLength(64);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Reading>(e =>
        {
            e.HasIndex(x => new { x.DeviceId, x.At });
            e.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DeviceEvent>(e =>
        {
            e.HasIndex(x => new { x.DeviceId, x.At });
            e.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Subscription>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique(); // one subscription per customer
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            // Same customer + same idempotency key must resolve to the same payment.
            e.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
            e.Property(x => x.Reference).HasMaxLength(64);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.HasIndex(x => x.At);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<SupportTicket>(e =>
        {
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => new { x.UserId, x.UpdatedAt });
            e.Property(x => x.Reference).HasMaxLength(32);
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.Subject).HasMaxLength(160);
            e.Property(x => x.Category).HasMaxLength(80);
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<SupportMessage>(e =>
        {
            e.HasIndex(x => new { x.TicketId, x.CreatedAt });
            e.Property(x => x.AuthorId).HasMaxLength(450);
            e.Property(x => x.Body).HasMaxLength(4000);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<RecurringBillingAgreement>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("IX_RecurringBillingAgreements_UserId_Current")
                .HasFilter("\"Status\" IN ('pending_card','finalizing','trialing','active','non_renewing','past_due')");
            e.HasIndex(x => x.PaystackSubscriptionCode).IsUnique();
            e.HasIndex(x => x.VerificationReference).IsUnique();
            e.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.PlanId).HasMaxLength(32);
            e.Property(x => x.PaystackPlanCode).HasMaxLength(64);
            e.Property(x => x.PaystackCustomerCode).HasMaxLength(64);
            e.Property(x => x.PaystackSubscriptionCode).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.VerificationReference).HasMaxLength(64);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
            e.Property(x => x.CardBrand).HasMaxLength(32);
            e.Property(x => x.CardLast4).HasMaxLength(4);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RecurringBillingTransaction>(e =>
        {
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => new { x.AgreementId, x.CreatedAt });
            e.Property(x => x.Reference).HasMaxLength(64);
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.PlanId).HasMaxLength(32);
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.Channel).HasMaxLength(32);
            e.Property(x => x.RefundStatus).HasMaxLength(32);
            e.Property(x => x.RefundReference).HasMaxLength(64);
            e.HasOne<RecurringBillingAgreement>().WithMany().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CustomerNotification>(e =>
        {
            e.HasIndex(x => x.DedupeKey).IsUnique();
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.Category).HasMaxLength(40);
            e.Property(x => x.Destination).HasMaxLength(240);
            e.Property(x => x.DedupeKey).HasMaxLength(160);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
