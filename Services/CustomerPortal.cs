using HydroSentinel.Backend;
using HydroSentinel.Data;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Services;

public sealed record CustomerDeviceVm(
    Guid Id,
    string Name,
    string Serial,
    string SerialMasked,
    string Status,
    DateTimeOffset? LastSeenAt,
    string? TankLevel,
    int? LevelPercent,
    decimal? PressureBar,
    bool? PumpOn,
    bool? MainsOn,
    int? FaultCode,
    int? BatteryPercent,
    int? SignalCsq,
    bool? PumpLocked,
    SubscriptionState SubscriptionState);

public sealed record CustomerAlertVm(
    long Id,
    Guid DeviceId,
    string DeviceName,
    string Type,
    string TypeLabel,
    string Severity,
    string? Detail,
    DateTimeOffset At,
    bool Acknowledged);

public sealed record CustomerReadingVm(
    DateTimeOffset At,
    string TankLevel,
    int? LevelPercent,
    decimal? PressureBar,
    bool PumpOn,
    bool MainsOn,
    int FaultCode,
    int? BatteryPercent,
    int? SignalCsq);

public sealed record CustomerSubscriptionVm(
    SubscriptionState State,
    string? PlanId,
    string? PlanName,
    bool? IsTrial,
    DateTimeOffset? PaidUntil,
    DateTimeOffset? GraceEndsAt)
{
    public bool Exists => State != SubscriptionState.None;
}

public sealed record CustomerDashboardVm(
    string FullName,
    IReadOnlyList<CustomerDeviceVm> Devices,
    IReadOnlyList<CustomerAlertVm> RecentAlerts,
    int OpenAlertCount,
    CustomerSubscriptionVm Subscription,
    DateTimeOffset Now);

/// <summary>
/// Read side of the customer portal. Every query is scoped by the signed-in user id,
/// so a page can never show another customer's device by guessing an id.
/// </summary>
public sealed class CustomerPortal(AppDbContext db, PlanCatalog plans)
{
    public static string Mask(string serial)
        => serial.Length <= 4 ? "****" : new string('*', serial.Length - 4) + serial[^4..];

    public static string LevelLabel(string? level) => level?.ToLowerInvariant() switch
    {
        "low" => "Low",
        "mid" => "Mid",
        "full" => "Full",
        _ => "Unknown"
    };

    public static string StateLabel(SubscriptionState state) => state switch
    {
        SubscriptionState.Active => "Active",
        SubscriptionState.Grace => "Grace period",
        SubscriptionState.Expired => "Expired",
        _ => "No subscription"
    };

    public static string TypeLabel(string type) => type switch
    {
        "tank_low" => "Low water level",
        "tank_full" => "Tank full",
        "battery_low" => "Battery low",
        "mains_lost" => "Mains power lost",
        "mains_restored" => "Mains power restored",
        "pump_on" => "Pump started",
        "pump_off" => "Pump stopped",
        "pump_locked" => "Pump locked",
        "pump_unlocked" => "Pump unlocked",
        "fault" => "Controller fault",
        "back_online" => "Back online",
        "went_offline" => "Went offline",
        _ => type.Replace('_', ' ')
    };

    public static string PressureLabel(decimal? bar) => bar is null ? "Not reported" : $"{bar.Value:0.0} bar";

    private CustomerDeviceVm ToVm(Device d, Subscription? sub, DateTimeOffset now)
    {
        return new CustomerDeviceVm(d.Id, d.Name, d.Serial, Mask(d.Serial), BillingRules.Freshness(d, now), d.LastSeenAt,
            d.TankLevel, d.LevelPercent, d.PressureBar, d.PumpOn, d.MainsOn, d.FaultCode, d.BatteryPercent,
            d.SignalCsq, d.ReportedLock, BillingRules.StateOf(sub, now));
    }

    public async Task<CustomerSubscriptionVm> SubscriptionAsync(string userId, CancellationToken ct = default)
    {
        var sub = await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var state = BillingRules.StateOf(sub, DateTimeOffset.UtcNow);
        return new CustomerSubscriptionVm(state, sub?.PlanId,
            sub is null ? null : plans.Find(sub.PlanId)?.Name ?? sub.PlanId,
            sub?.IsTrial, sub?.PaidUntil, sub is null ? null : sub.PaidUntil + BillingRules.GracePeriod);
    }

    public async Task<List<CustomerDeviceVm>> DevicesAsync(string userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var sub = await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var devices = await db.Devices.AsNoTracking().Where(d => d.OwnerId == userId).OrderBy(d => d.Name).ToListAsync(ct);
        return devices.Select(d => ToVm(d, sub, now)).ToList();
    }

    public async Task<CustomerDeviceVm?> DeviceAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var sub = await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var d = await db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, ct);
        return d is null ? null : ToVm(d, sub, now);
    }

    public async Task<List<CustomerAlertVm>> AlertsAsync(string userId, string? severity = null, string? status = null,
        string? search = null, int limit = 100, CancellationToken ct = default)
    {
        var q = from e in db.DeviceEvents.AsNoTracking()
                join d in db.Devices.AsNoTracking() on e.DeviceId equals d.Id
                where d.OwnerId == userId && e.Severity != "info"
                select new { e, d.Name };

        if (status == "open") q = q.Where(x => x.e.AcknowledgedAt == null);
        if (status == "acknowledged") q = q.Where(x => x.e.AcknowledgedAt != null);
        if (severity is "critical" or "warning") q = q.Where(x => x.e.Severity == severity);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(x => x.e.Type.Contains(search) || (x.e.Detail != null && x.e.Detail.Contains(search)) || x.Name.Contains(search));

        var rows = await q.OrderByDescending(x => x.e.At).Take(Math.Clamp(limit, 1, 200)).ToListAsync(ct);
        return rows.Select(x => new CustomerAlertVm(x.e.Id, x.e.DeviceId, x.Name, x.e.Type, TypeLabel(x.e.Type),
            x.e.Severity, x.e.Detail, x.e.At, x.e.AcknowledgedAt is not null)).ToList();
    }

    public async Task<List<CustomerAlertVm>> AlertsForDeviceAsync(string userId, Guid deviceId, int limit = 5, CancellationToken ct = default)
    {
        var all = await AlertsAsync(userId, limit: 200, ct: ct);
        return all.Where(a => a.DeviceId == deviceId).Take(limit).ToList();
    }

    public async Task<CustomerAlertVm?> AlertAsync(string userId, long id, CancellationToken ct = default)
    {
        var row = await (from e in db.DeviceEvents.AsNoTracking()
                         join d in db.Devices.AsNoTracking() on e.DeviceId equals d.Id
                         where e.Id == id && d.OwnerId == userId
                         select new { e, d.Name }).FirstOrDefaultAsync(ct);
        return row is null ? null : new CustomerAlertVm(row.e.Id, row.e.DeviceId, row.Name, row.e.Type, TypeLabel(row.e.Type),
            row.e.Severity, row.e.Detail, row.e.At, row.e.AcknowledgedAt is not null);
    }

    /// <summary>Event log for one device, including informational transitions (pump on/off, back online).</summary>
    public async Task<List<CustomerAlertVm>> DeviceEventsAsync(string userId, Guid deviceId, int limit = 20, CancellationToken ct = default)
    {
        var owned = await db.Devices.AsNoTracking().AnyAsync(d => d.Id == deviceId && d.OwnerId == userId, ct);
        if (!owned) return [];

        var rows = await (from e in db.DeviceEvents.AsNoTracking()
                          join d in db.Devices.AsNoTracking() on e.DeviceId equals d.Id
                          where e.DeviceId == deviceId
                          orderby e.At descending
                          select new { e, d.Name }).Take(Math.Clamp(limit, 1, 100)).ToListAsync(ct);
        return rows.Select(x => new CustomerAlertVm(x.e.Id, x.e.DeviceId, x.Name, x.e.Type, TypeLabel(x.e.Type),
            x.e.Severity, x.e.Detail, x.e.At, x.e.AcknowledgedAt is not null)).ToList();
    }

    public async Task<List<CustomerReadingVm>> ReadingsAsync(string userId, Guid deviceId, DateTimeOffset from, DateTimeOffset to,
        int limit = 200, CancellationToken ct = default)
    {
        var owned = await db.Devices.AsNoTracking().AnyAsync(d => d.Id == deviceId && d.OwnerId == userId, ct);
        if (!owned) return [];

        var rows = await db.Readings.AsNoTracking()
            .Where(r => r.DeviceId == deviceId && r.At >= from && r.At <= to)
            .OrderByDescending(r => r.At).Take(Math.Clamp(limit, 1, 200)).ToListAsync(ct);
        return rows.Select(r => new CustomerReadingVm(r.At, r.TankLevel, r.LevelPercent, r.PressureBar, r.PumpOn,
            r.MainsOn, r.FaultCode, r.BatteryPercent, r.SignalCsq)).ToList();
    }

    public async Task<string> FullNameAsync(string userId, CancellationToken ct = default)
        => await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Customer";

    public async Task<int> OpenAlertCountAsync(string userId, CancellationToken ct = default)
        => await (from e in db.DeviceEvents.AsNoTracking()
                  join d in db.Devices.AsNoTracking() on e.DeviceId equals d.Id
                  where d.OwnerId == userId && e.Severity != "info" && e.AcknowledgedAt == null
                  select e).CountAsync(ct);

    public async Task<CustomerDashboardVm> DashboardAsync(string userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var devices = await DevicesAsync(userId, ct);
        var alerts = await AlertsAsync(userId, status: "open", limit: 3, ct: ct);
        var openCount = await OpenAlertCountAsync(userId, ct);
        var sub = await SubscriptionAsync(userId, ct);
        return new CustomerDashboardVm(await FullNameAsync(userId, ct), devices, alerts, openCount, sub, now);
    }
}
