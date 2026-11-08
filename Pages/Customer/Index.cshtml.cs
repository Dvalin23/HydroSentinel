using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Customer;

public class IndexModel(CustomerPortal portal) : CustomerPageModel
{
    public CustomerDashboardVm Data { get; private set; } = default!;
    public int OnlineCount => Data.Devices.Count(d => d.Status == "live");
    public CustomerDeviceVm? PrimaryDevice => Data.Devices.FirstOrDefault();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();
        Data = await portal.DashboardAsync(uid, ct);
        return Page();
    }
}
