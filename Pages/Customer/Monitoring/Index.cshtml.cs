using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using HydroSentinel.Services;

namespace HydroSentinel.Pages.Customer.Monitoring;

public class IndexModel(IDemoMode demo) : PageModel
{
    public MonitoringViewState CurrentState { get; private set; } = MonitoringViewState.Unavailable;

    public void OnGet() { }
    public DemoSnapshot Data => demo.Snapshot;
    public DemoDevice? SelectedDevice => Data.Devices.FirstOrDefault(x => x.Id == DeviceId) ?? Data.Devices.FirstOrDefault();
    [BindProperty(SupportsGet = true)] public string? DeviceId { get; set; }
    [BindProperty(SupportsGet = true)] public string Range { get; set; } = "24h";
}

public enum MonitoringViewState
{
    Loading,
    Populated,
    Empty,
    Unavailable,
    RequestFailure,
    Offline,
    Stale
}
