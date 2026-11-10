using System.Security.Claims;
using HydroSentinel.Backend;
using HydroSentinel.Data;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Pages.Customer.Payments;

public class ResultModel(IDemoMode demo, AppDbContext db, PaymentService payments, RecurringBillingService recurring) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "reference")]
    public string? Reference { get; set; }

    public bool Live => !demo.Enabled;
    public bool HasReference => !string.IsNullOrWhiteSpace(Reference) && Reference.Length <= 256;
    public bool HasInvalidReference => !string.IsNullOrWhiteSpace(Reference) && Reference.Length > 256;
    public DemoPayment? Payment => demo.Snapshot.Payments.FirstOrDefault(x => x.Reference.Equals(Reference, StringComparison.OrdinalIgnoreCase));

    /// <summary>Server-verified payment for the signed-in customer. Null if not theirs or not found.</summary>
    public HydroSentinel.Data.Payment? LivePayment { get; private set; }
    public bool UnableToVerify { get; private set; }
    public RecurringBillingTransaction? RecurringTransaction { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (!Live || !HasReference || HasInvalidReference) return;
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (uid is null) return;

        var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Reference == Reference && x.UserId == uid, ct);
        if (p is null)
        {
            RecurringTransaction = await db.RecurringBillingTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.Reference == Reference && x.UserId == uid, ct);
            if (RecurringTransaction is { Status: PaymentStatus.Pending })
            {
                UnableToVerify = await recurring.SettleReferenceAsync(Reference!, ct) == false;
                RecurringTransaction = await db.RecurringBillingTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.Reference == Reference && x.UserId == uid, ct);
            }
            return;
        }
        if (p.Status == PaymentStatus.Pending)
        {
            var outcome = await payments.VerifyAndSettleAsync(p.Reference, ct);
            UnableToVerify = outcome == SettleOutcome.Unverifiable;
            p = await db.Payments.AsNoTracking().FirstAsync(x => x.Reference == p.Reference, ct);
        }
        LivePayment = p;
    }
}
