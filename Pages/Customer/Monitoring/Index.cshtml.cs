using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;

namespace HydroSentinel.Pages.Customer.Monitoring;

public class IndexModel(CustomerPortal portal) : CustomerPageModel
{
    [BindProperty(SupportsGet = true)] public string? DeviceId { get; set; }
    [BindProperty(SupportsGet = true)] public string Range { get; set; } = "24h";

    public IReadOnlyList<CustomerDeviceVm> Devices { get; private set; } = [];
    public CustomerDeviceVm? SelectedDevice { get; private set; }
    public IReadOnlyList<CustomerReadingVm> Readings { get; private set; } = [];
    public DateTimeOffset From { get; private set; }
    public DateTimeOffset To { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();

        Devices = await portal.DevicesAsync(uid, ct);
        SelectedDevice = RequestedDeviceId(DeviceId) is { } id
            ? Devices.FirstOrDefault(d => d.Id == id)
            : Devices.FirstOrDefault();

        To = DateTimeOffset.UtcNow;
        if (Range is not ("24h" or "7d")) Range = "24h";
        From = Range == "7d" ? To.AddDays(-7) : To.AddDays(-1);

        if (SelectedDevice is not null)
            Readings = await portal.ReadingsAsync(uid, SelectedDevice.Id, From, To, 200, ct);

        return Page();
    }
}
