using HydroSentinel.Services;
using HydroSentinel.Backend;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace HydroSentinel.Pages.Customer.Subscription;

public class PlansModel(PlanCatalog catalog, IDemoMode demo, PaystackClient paystack, IOptions<PaystackOptions> paystackOptions) : PageModel
{
    public IReadOnlyList<PlanDefinition> Plans => catalog.Plans;
    public string OfferNote => PlanCatalog.OfferNote;
    public bool DemoMode => demo.Enabled;
    public bool TestCheckout => paystack.UsesTestKey;

    /// <summary>Checkout is ready only when demo mode is off and an environment-appropriate key is configured.</summary>
    public bool CheckoutReady => !DemoMode && paystack.Configured && Plans.All(p => paystackOptions.Value.PlanCodeFor(p.Id) is not null);
}
