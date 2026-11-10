using System.Security.Claims;
using HydroSentinel.Backend;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HydroSentinel.Services;

namespace HydroSentinel.Pages.Customer.Payments;

public class CheckoutModel(IDemoMode demo, PlanCatalog catalog, RecurringBillingService recurring) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "plan")]
    public string? Plan { get; set; }
    [BindProperty] public string Outcome { get; set; } = "success";

    // Live mode (Demo disabled): the browser sends only plan, months and an idempotency key.
    // The amount is computed on the server from PlanCatalog and is never read from the request.
    [BindProperty] public int Months { get; set; } = 1;
    [BindProperty] public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    [BindProperty] public bool AcceptedRecurringTerms { get; set; }

    public bool Live => !demo.Enabled;
    public PlanDefinition? SelectedPlan => catalog.Find(Plan);
    public decimal LiveTotal => SelectedPlan?.MonthlyPrice ?? 0;
    public string? Message { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (SelectedPlan is null) return Page();

        if (!Live)
        {
            var payment = demo.SimulatePayment(SelectedPlan.Id, Outcome);
            return RedirectToPage("/Customer/Payments/Result", new { reference = payment.Reference });
        }

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (uid is null) return Challenge();

        var r = await recurring.StartAsync(uid, SelectedPlan.Id, IdempotencyKey, AcceptedRecurringTerms, $"{Request.Scheme}://{Request.Host}", ct);
        if (!r.Ok)
        {
            Message = r.Message;
            IdempotencyKey = Guid.NewGuid().ToString("N"); // a failed attempt should not poison a retry
            return Page();
        }
        if (r.RedirectUrl is not null) return Redirect(r.RedirectUrl);
        // Replay of an earlier key: go to the result page, which verifies server-side.
        return RedirectToPage("/Customer/Payments/Result", new { reference = r.Reference });
    }
}
