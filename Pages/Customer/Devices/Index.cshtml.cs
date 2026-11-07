using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Customer.Devices;

public class IndexModel(IDemoMode demo) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    public DemoSnapshot Data => demo.Snapshot;
    public string? Message { get; private set; }
    public IEnumerable<DemoDevice> Devices => Data.Devices.Where(x => (string.IsNullOrWhiteSpace(Search) || x.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || x.Serial.Contains(Search, StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrWhiteSpace(Status) || Status == "All" || x.Status.Equals(Status, StringComparison.OrdinalIgnoreCase)));
    public IActionResult OnPostRemove(string id) { if (!demo.Enabled) return NotFound(); Message = demo.RemoveDevice(id) ? "Demo device removed. No hardware record was changed." : "That demo device could not be found."; return Page(); }
}
