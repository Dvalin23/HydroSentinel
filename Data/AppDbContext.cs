using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<DeviceEvent> DeviceEvents => Set<DeviceEvent>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

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
        });

        b.Entity<Reading>(e =>
        {
            e.HasIndex(x => new { x.DeviceId, x.At });
        });

        b.Entity<DeviceEvent>(e =>
        {
            e.HasIndex(x => new { x.DeviceId, x.At });
        });

        b.Entity<Subscription>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique(); // one subscription per customer
        });

        b.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.Reference).IsUnique();
            // Same customer + same idempotency key must resolve to the same payment.
            e.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
            e.Property(x => x.Reference).HasMaxLength(64);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
        });
    }
}
