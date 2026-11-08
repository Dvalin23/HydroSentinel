using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;

namespace HydroSentinel.Pages.Customer.Subscription;

public class IndexModel(CustomerPortal portal, PlanCatalog catalog) : CustomerPageModel
{
    public CustomerSubscriptionVm Subscription { get; private set; } = default!;
    public PlanDefinition? CurrentPlan => catalog.Find(Subscription.PlanId);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();
        Subscription = await portal.SubscriptionAsync(uid, ct);
        return Page();
    }
}
