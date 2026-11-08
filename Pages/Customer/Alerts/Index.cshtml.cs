using HydroSentinel.Data;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Pages.Customer.Alerts;

public class IndexModel(CustomerPortal portal, AppDbContext db) : CustomerPageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Severity { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }

    public IReadOnlyList<CustomerAlertVm> Alerts { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();

        Alerts = await portal.AlertsAsync(uid, Severity, Status, Search, 100, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAcknowledgeAsync(long id, CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();

        var ev = await (from e in db.DeviceEvents join d in db.Devices on e.DeviceId equals d.Id
                        where e.Id == id && d.OwnerId == uid select e).FirstOrDefaultAsync(ct);
        if (ev is null) return NotFound();

        if (ev.AcknowledgedAt is null)
        {
            ev.AcknowledgedAt = DateTimeOffset.UtcNow;
            ev.AcknowledgedBy = uid;
            await db.SaveChangesAsync(ct);
        }
        return RedirectToPage(new { Search, Severity, Status });
    }
}
