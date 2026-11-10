using HydroSentinel.Data;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Services;

/// <summary>Customer-scoped in-app notification storage. This service never sends email, SMS, or push.</summary>
public sealed class NotificationService(AppDbContext db)
{
    public async Task<bool> AddAsync(string userId, string title, string message, string category, string dedupeKey,
        string? destination = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(dedupeKey)) return false;
        if (await db.CustomerNotifications.AnyAsync(x => x.DedupeKey == dedupeKey, ct)) return false;

        var notification = new CustomerNotification
        {
            UserId = userId,
            Title = title.Trim()[..Math.Min(title.Trim().Length, 160)],
            Message = (message ?? string.Empty).Trim()[..Math.Min((message ?? string.Empty).Trim().Length, 1000)],
            Category = string.IsNullOrWhiteSpace(category) ? "Account" : category.Trim()[..Math.Min(category.Trim().Length, 40)],
            DedupeKey = dedupeKey.Trim()[..Math.Min(dedupeKey.Trim().Length, 160)],
            Destination = SafeDestination(destination)
        };
        db.CustomerNotifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(notification).State = EntityState.Detached;
            // A duplicate source event can race with a webhook/device retry; its unique key makes this harmless.
            return false;
        }
    }

    public Task<List<CustomerNotification>> ForUserAsync(string userId, int take = 100, CancellationToken ct = default)
        => db.CustomerNotifications.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);

    public Task<int> UnreadCountAsync(string userId, CancellationToken ct = default)
        => db.CustomerNotifications.CountAsync(x => x.UserId == userId && !x.IsRead, ct);

    public async Task<bool> MarkReadAsync(string userId, Guid id, CancellationToken ct = default)
        => await db.CustomerNotifications.Where(x => x.UserId == userId && x.Id == id && !x.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAt, DateTimeOffset.UtcNow), ct) > 0;

    public async Task<int> MarkAllReadAsync(string userId, CancellationToken ct = default)
        => await db.CustomerNotifications.Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true).SetProperty(x => x.ReadAt, DateTimeOffset.UtcNow), ct);

    private static string? SafeDestination(string? destination)
        => !string.IsNullOrWhiteSpace(destination) && destination.StartsWith('/') && !destination.StartsWith("//")
            && !destination.Contains('\\') ? destination[..Math.Min(destination.Length, 240)] : null;
}
