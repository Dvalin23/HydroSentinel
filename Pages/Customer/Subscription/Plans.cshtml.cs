using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Customer.Subscription;

public class PlansModel(PlanCatalog catalog, IConfiguration configuration) : PageModel
{
    public IReadOnlyList<PlanDefinition> Plans => catalog.Plans;
    public string OfferNote => PlanCatalog.OfferNote;

    /// <summary>Checkout calls Paystack server-side; without a key it will fail with an honest error.</summary>
    public bool CheckoutReady => !string.IsNullOrWhiteSpace(configuration["Paystack:SecretKey"]);
}
