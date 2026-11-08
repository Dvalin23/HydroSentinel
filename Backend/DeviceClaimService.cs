using HydroSentinel.Data;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Backend;

public enum ClaimOutcome
{
    Claimed,
    AlreadyOwned,
    Rejected,
    Conflict,
    Invalid
}

public sealed record ClaimResult(ClaimOutcome Outcome, Device? Device);

/// <summary>
/// A customer binds an unassigned unit to their own account with the serial printed on it
/// plus the one-time device key. Ownership is proved by knowledge of the key, so a wrong key
/// and an unknown serial return the same answer.
/// </summary>
public sealed class DeviceClaimService(AppDbContext db, AuditService audit)
{
    public async Task<ClaimResult> ClaimAsync(string userId, string? serial, string? key, CancellationToken ct)
    {
        serial = serial?.Trim();
        key = key?.Trim();
        if (serial is null || key is null || serial.Length is < 3 or > 64 || key.Length is < 8 or > 128)
            return new ClaimResult(ClaimOutcome.Invalid, null);

        var device = await db.Devices.FirstOrDefaultAsync(d => d.Serial == serial, ct);
        if (device is null || !DeviceKeys.Matches(key, device.KeyHash))
            return new ClaimResult(ClaimOutcome.Rejected, null);

        if (device.OwnerId == userId)
            return new ClaimResult(ClaimOutcome.AlreadyOwned, device);

        if (device.OwnerId is not null)
            return new ClaimResult(ClaimOutcome.Conflict, device);

        device.OwnerId = userId;
        await db.EnsureTrialAsync(userId, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(userId, "device.claim", device.Id.ToString(), device.Serial, ct);
        return new ClaimResult(ClaimOutcome.Claimed, device);
    }
}
