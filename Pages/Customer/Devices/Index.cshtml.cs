using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;

namespace HydroSentinel.Pages.Customer.Devices;

public class IndexModel(CustomerPortal portal) : CustomerPageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }

    public IReadOnlyList<CustomerDeviceVm> Devices { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();

        var all = await portal.DevicesAsync(uid, ct);
        Devices = all.Where(d =>
            (string.IsNullOrWhiteSpace(Search) || d.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || d.Serial.Contains(Search, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(Status) || Status == "All" || d.Status.Equals(Status, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return Page();
    }
}
