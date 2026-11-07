using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HydroSentinel.Services;

namespace HydroSentinel.Pages.Customer.Alerts;

public class DetailsModel(IDemoMode demo) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "id")]
    public string? AlertId { get; set; }

    public bool HasRequestedId => !string.IsNullOrWhiteSpace(AlertId) && AlertId!.Length <= 128;
    public bool HasInvalidId => !string.IsNullOrWhiteSpace(AlertId) && AlertId!.Length > 128;
    public DemoAlert? Alert => demo.Snapshot.Alerts.FirstOrDefault(x => x.Id == AlertId);
}
