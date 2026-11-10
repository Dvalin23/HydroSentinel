using HydroSentinel.Services;
using HydroSentinel.Data;
using HydroSentinel.Backend;
using Microsoft.AspNetCore.Mvc;

namespace HydroSentinel.Pages.Customer.Subscription;

public class IndexModel(CustomerPortal portal, PlanCatalog catalog, RecurringBillingService recurring) : CustomerPageModel
{
    public CustomerSubscriptionVm Subscription { get; private set; } = default!;
    public PlanDefinition? CurrentPlan => catalog.Find(Subscription.PlanId);
    public RecurringBillingAgreement? BillingAgreement { get; private set; }
    public string? BillingMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();
        Subscription = await portal.SubscriptionAsync(uid, ct);
        BillingAgreement = await recurring.CurrentAsync(uid, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostManageAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();
        var link = await recurring.ManagementLinkAsync(uid, ct);
        if (link is not null) return Redirect(link);
        Subscription = await portal.SubscriptionAsync(uid, ct);
        BillingAgreement = await recurring.CurrentAsync(uid, ct);
        BillingMessage = "Subscription management is temporarily unavailable. Please try again or contact support.";
        return Page();
    }
}
