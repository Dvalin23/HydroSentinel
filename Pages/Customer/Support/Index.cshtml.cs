using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Customer.Support;

public class IndexModel(IDemoMode demo) : PageModel
{
    public DemoSnapshot Data => demo.Snapshot;
}
