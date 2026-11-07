using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using HydroSentinel.Services;

namespace HydroSentinel.Pages.Customer.Alerts;

public class IndexModel(IDemoMode demo) : PageModel
{
    public AlertViewState CurrentState { get; private set; } = AlertViewState.Unavailable;

    public void OnGet() { }
    public DemoSnapshot Data => demo.Snapshot;
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Severity { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    public IEnumerable<DemoAlert> Alerts => Data.Alerts.Where(x => (string.IsNullOrWhiteSpace(Search) || x.Title.Contains(Search, StringComparison.OrdinalIgnoreCase) || x.Description.Contains(Search, StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrWhiteSpace(Severity) || Severity == "All" || x.Severity.Equals(Severity, StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrWhiteSpace(Status) || Status == "All" || x.Status.Equals(Status, StringComparison.OrdinalIgnoreCase)));
    public IActionResult OnPostAcknowledge(string id) { if (!demo.Enabled) return NotFound(); demo.AcknowledgeAlert(id); return RedirectToPage(new { Search, Severity, Status }); }
}

public enum AlertViewState
{
    Loading,
    Populated,
    Empty,
    Unavailable,
    RequestFailure,
    Offline,
    Stale
}
