using Microsoft.AspNetCore.Identity;

namespace HydroSentinel.Data;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Printed on the unit. Unique. Used as the "id" the controller sends.</summary>
    public string Serial { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Customer who owns the unit. Null = in stock / unassigned.</summary>
    public string? OwnerId { get; set; }
    /// <summary>SHA-256 (hex) of the device key. The raw key is shown once at registration.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Operator-set lock. Combined with subscription state to get the effective lock.</summary>
    public bool AdminLock { get; set; }
    public string? AdminLockReason { get; set; }

    // Last known state, from the most recent check-in.
    public DateTimeOffset? LastSeenAt { get; set; }
    public string? TankLevel { get; set; }       // "low" | "mid" | "full"
    public bool? PumpOn { get; set; }
    public bool? MainsOn { get; set; }
    public int? FaultCode { get; set; }
    public int? BatteryPercent { get; set; }
    public int? SignalCsq { get; set; }
    /// <summary>What the device itself last reported as its lock state.</summary>
    public bool? ReportedLock { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Reading
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset At { get; set; }
    public string TankLevel { get; set; } = "low";
    public bool PumpOn { get; set; }
    public bool MainsOn { get; set; }
    public int FaultCode { get; set; }
    public int? BatteryPercent { get; set; }
    public int? SignalCsq { get; set; }
}

public class DeviceEvent
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset At { get; set; }
    /// <summary>tank_full, tank_low, pump_on, pump_off, mains_lost, mains_restored, battery_low, fault, back_online, went_offline, pump_locked, pump_unlocked</summary>
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "info"; // critical | warning | info
    public string? Detail { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
}

public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string PlanId { get; set; } = "basic";
    /// <summary>Service runs to this instant (UTC). Past it: grace period, then locked.</summary>
    public DateTimeOffset PaidUntil { get; set; }
    public bool IsTrial { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class PaymentStatus
{
    public const string Pending = "pending";
    public const string Success = "success";
    public const string Failed = "failed";
    public const string AmountMismatch = "amount_mismatch";
}

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public int Months { get; set; } = 1;
    /// <summary>Minor units (kobo). Set by the server from the PlanCatalog, never by the client.</summary>
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "NGN";
    public string Status { get; set; } = PaymentStatus.Pending;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }
}

public class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public string? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Detail { get; set; }
    public string? CorrelationId { get; set; }
}
