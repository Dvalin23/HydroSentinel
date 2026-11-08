using HydroSentinel.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HydroSentinel.Backend;

public sealed record CheckInInput(string Serial, string Key, string? Water, bool Pump, bool Mains, int Fault, bool DeviceLock, int? Battery, int? Signal, int? LevelPercent = null, int? PressureCentiBar = null);
public sealed record CheckInResult(bool Authenticated, bool Lock);

/// <summary>Handles one controller check-in: authenticate, store, diff into events, and answer with the lock state.</summary>
public sealed class CheckInService(AppDbContext db, IOptions<FeatureOptions> features)
{
    private static string? LevelOf(string? w) => w?.ToUpperInvariant() switch { "L" => "low", "M" => "mid", "F" => "full", _ => null };

    // Percent and centibar are sent as integers so the SIM800L never has to format a float.
    private static int? PercentOf(int? v) => v is >= 0 and <= 100 ? v : null;
    private static decimal? BarOf(int? centibar) => centibar is >= 0 and <= 2000 ? centibar.Value / 100m : null;

    public async Task<CheckInResult> HandleAsync(CheckInInput i, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Serial == i.Serial, ct);
        if (device is null || !DeviceKeys.Matches(i.Key, device.KeyHash))
            return new CheckInResult(false, false);

        var level = LevelOf(i.Water);
        if (level is null) return new CheckInResult(false, false); // malformed -> treated as bad request by caller

        var percent = PercentOf(i.LevelPercent);
        var pressure = BarOf(i.PressureCentiBar);

        var wasOffline = BillingRules.IsOffline(device, now);
        var hadSeen = device.LastSeenAt is not null;
        var events = new List<DeviceEvent>();
        void Ev(string type, string severity, string? detail = null)
            => events.Add(new DeviceEvent { DeviceId = device.Id, At = now, Type = type, Severity = severity, Detail = detail });

        if (hadSeen && wasOffline) Ev("back_online", "info");
        if (device.TankLevel is not null && device.TankLevel != level)
        {
            if (level == "full") Ev("tank_full", "info");
            if (level == "low") Ev("tank_low", "warning");
        }
        if (device.PumpOn is not null && device.PumpOn != i.Pump) Ev(i.Pump ? "pump_on" : "pump_off", "info");
        if (device.MainsOn is not null && device.MainsOn != i.Mains) Ev(i.Mains ? "mains_restored" : "mains_lost", i.Mains ? "info" : "warning");
        if (i.Battery is int b && b <= 20 && (device.BatteryPercent is null || device.BatteryPercent > 20)) Ev("battery_low", "warning", $"{b}%");
        if (i.Fault != 0 && (device.FaultCode ?? 0) != i.Fault) Ev("fault", "critical", $"code {i.Fault}");

        // What lock should the device hold? Only applied when the remote-lock feature is approved and on.
        var wantLock = false;
        string? cause = null;
        if (features.Value.RemoteLockEnabled)
        {
            Subscription? sub = device.OwnerId is null ? null : await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == device.OwnerId, ct);
            wantLock = BillingRules.EffectiveLock(device, sub, now);
            cause = BillingRules.LockCause(device, sub, now);
        }

        if (device.ReportedLock is not null && device.ReportedLock != wantLock)
            Ev(wantLock ? "pump_locked" : "pump_unlocked", wantLock ? "warning" : "info", wantLock ? $"cause: {cause}" : null);

        device.LastSeenAt = now;
        device.TankLevel = level;
        device.LevelPercent = percent;
        device.PressureBar = pressure;
        device.PumpOn = i.Pump;
        device.MainsOn = i.Mains;
        device.FaultCode = i.Fault;
        device.BatteryPercent = i.Battery;
        device.SignalCsq = i.Signal;
        device.ReportedLock = i.DeviceLock;

        db.Readings.Add(new Reading
        {
            DeviceId = device.Id, At = now, TankLevel = level, LevelPercent = percent, PressureBar = pressure,
            PumpOn = i.Pump, MainsOn = i.Mains,
            FaultCode = i.Fault, BatteryPercent = i.Battery, SignalCsq = i.Signal
        });
        db.DeviceEvents.AddRange(events);
        await db.SaveChangesAsync(ct);
        return new CheckInResult(true, wantLock);
    }
}
