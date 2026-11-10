using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;

namespace HydroSentinel.Pages.Customer;

public sealed record NotificationItemVm(string Id, string Title, string Message, string Category, DateTimeOffset CreatedAt, bool IsRead, string? Destination);

public class NotificationsModel(IDemoMode demo, NotificationService notifications) : CustomerPageModel
{
    public bool DemoMode => demo.Enabled;
    public IReadOnlyList<NotificationItemVm> Items { get; private set; } = [];
    public int UnreadCount => Items.Count(x => !x.IsRead);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (DemoMode)
        {
            Items = demo.Snapshot.Notifications.Select(x => new NotificationItemVm(x.Id, x.Title, x.Message, x.Category, x.Created, x.IsRead, x.Destination)).ToList();
            return Page();
        }
        var uid = UserId;
        if (uid is null) return SignInRequired();
        Items = (await notifications.ForUserAsync(uid, ct: ct))
            .Select(x => new NotificationItemVm(x.Id.ToString(), x.Title, x.Message, x.Category, x.CreatedAt, x.IsRead, x.Destination)).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostReadAsync(string? id, CancellationToken ct)
    {
        if (DemoMode)
        {
            if (!string.IsNullOrWhiteSpace(id)) demo.MarkNotificationRead(id);
            return RedirectToPage();
        }
        var uid = UserId;
        if (uid is null) return SignInRequired();
        if (Guid.TryParse(id, out var notificationId)) await notifications.MarkReadAsync(uid, notificationId, ct);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReadAllAsync(CancellationToken ct)
    {
        if (DemoMode) demo.MarkNotificationsRead();
        else
        {
            var uid = UserId;
            if (uid is null) return SignInRequired();
            await notifications.MarkAllReadAsync(uid, ct);
        }
        return RedirectToPage();
    }
}
