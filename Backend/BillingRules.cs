using HydroSentinel.Data;

namespace HydroSentinel.Backend;

public enum SubscriptionState { None, Active, Grace, Expired }

/// <summary>
/// Pure subscription/lock rules. No I/O, so it is easy to unit test.
/// </summary>
public static class BillingRules
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(3);
    public static readonly TimeSpan TrialLength = TimeSpan.FromDays(7);
    public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(45);
    public const int DaysPerMonth = 30;

    public static SubscriptionState StateOf(Subscription? sub, DateTimeOffset now)
    {
        if (sub is null) return SubscriptionState.None;
        if (now <= sub.PaidUntil) return SubscriptionState.Active;
        if (now <= sub.PaidUntil + GracePeriod) return SubscriptionState.Grace;
        return SubscriptionState.Expired;
    }

    /// <summary>
    /// Whether the pump should be held off.
    /// - Unassigned devices are never locked (nobody to bill).
    /// - Owned device with no subscription record is treated as expired.
    /// The lock only ever disables the pump; it never starts it.
    /// </summary>
    public static bool EffectiveLock(Device device, Subscription? ownerSub, DateTimeOffset now)
    {
        if (device.AdminLock) return true;
        if (device.OwnerId is null) return false;
        var s = StateOf(ownerSub, now);
        return s == SubscriptionState.Expired || s == SubscriptionState.None;
    }

    public static string? LockCause(Device device, Subscription? ownerSub, DateTimeOffset now)
    {
        if (device.AdminLock) return "admin";
        if (device.OwnerId is null) return null;
        var s = StateOf(ownerSub, now);
        return s == SubscriptionState.Expired || s == SubscriptionState.None ? "subscription" : null;
    }

    /// <summary>New paid-until: extend from the later of now and the current end date.</summary>
    public static DateTimeOffset Extend(DateTimeOffset? currentPaidUntil, int months, DateTimeOffset now)
    {
        var start = currentPaidUntil.HasValue && currentPaidUntil.Value > now ? currentPaidUntil.Value : now;
        return start.AddDays(DaysPerMonth * months);
    }

    public static bool IsOffline(Device d, DateTimeOffset now)
        => d.LastSeenAt is null || now - d.LastSeenAt.Value > OfflineAfter;

    /// <summary>Freshness label for APIs. Missing data is "unknown", never zero.</summary>
    public static string Freshness(Device d, DateTimeOffset now)
    {
        if (d.LastSeenAt is null) return "unknown";
        return IsOffline(d, now) ? "offline" : "live";
    }

    /// <summary>Customer-facing label for a Freshness() value.</summary>
    public static string FreshnessText(string status) => status switch
    {
        "live" => "Online",
        "offline" => "Offline",
        _ => "Awaiting first check-in"
    };

    /// <summary>Convert NGN to kobo without floating point.</summary>
    public static long ToMinor(decimal amount) => decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
}
