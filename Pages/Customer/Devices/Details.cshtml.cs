using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;

namespace HydroSentinel.Pages.Customer.Devices;

public class DetailsModel(CustomerPortal portal) : CustomerPageModel
{
    [BindProperty(SupportsGet = true, Name = "id")]
    public string? DeviceId { get; set; }

    [BindProperty(SupportsGet = true, Name = "claimed")]
    public bool Claimed { get; set; }

    public string? Message => TempData["DeviceClaimed"] as string;

    public CustomerDeviceVm Device { get; private set; } = default!;
    public IReadOnlyList<CustomerAlertVm> Alerts { get; private set; } = [];
    public IReadOnlyList<CustomerAlertVm> Events { get; private set; } = [];
    public IReadOnlyList<CustomerReadingVm> Readings { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();

        var id = RequestedDeviceId(DeviceId);
        if (id is null) return RedirectToPage("/Customer/Devices/Index");

        var device = await portal.DeviceAsync(uid, id.Value, ct);
        if (device is null) return NotFound();

        Device = device;
        Alerts = await portal.AlertsForDeviceAsync(uid, id.Value, 5, ct);
        Events = await portal.DeviceEventsAsync(uid, id.Value, 15, ct);
        Readings = await portal.ReadingsAsync(uid, id.Value, DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow, 25, ct);
        return Page();
    }
}
