using HydroSentinel.Data;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Pages.Customer.Alerts;

public class DetailsModel(CustomerPortal portal, AppDbContext db) : CustomerPageModel
{
    [BindProperty(SupportsGet = true, Name = "id")]
    public long AlertId { get; set; }

    public CustomerAlertVm Alert { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();

        var alert = await portal.AlertAsync(uid, AlertId, ct);
        if (alert is null) return NotFound();

        Alert = alert;
        return Page();
    }

    public async Task<IActionResult> OnPostAcknowledgeAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();

        var ev = await (from e in db.DeviceEvents join d in db.Devices on e.DeviceId equals d.Id
                        where e.Id == AlertId && d.OwnerId == uid select e).FirstOrDefaultAsync(ct);
        if (ev is null) return NotFound();

        if (ev.AcknowledgedAt is null)
        {
            ev.AcknowledgedAt = DateTimeOffset.UtcNow;
            ev.AcknowledgedBy = uid;
            await db.SaveChangesAsync(ct);
        }
        return RedirectToPage(new { id = AlertId });
    }
}
