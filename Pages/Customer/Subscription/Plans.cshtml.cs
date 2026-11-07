using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Customer.Subscription;

public class PlansModel(PlanCatalog catalog) : PageModel
{
    public IReadOnlyList<PlanDefinition> Plans => catalog.Plans;
    public string OfferNote => PlanCatalog.OfferNote;
}
