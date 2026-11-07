using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HydroSentinel.Services;

namespace HydroSentinel.Pages.Customer.Devices;

public class DetailsModel(IDemoMode demo) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "id")]
    public string? DeviceId { get; set; }

    public DeviceSelectionState SelectionState =>
        string.IsNullOrWhiteSpace(DeviceId)
            ? DeviceSelectionState.NoSelection
            : DeviceId.Length > 128
                ? DeviceSelectionState.InvalidRequest
                : DeviceSelectionState.AwaitingIntegration;

    public bool HasRequestedId => SelectionState != DeviceSelectionState.NoSelection;
    public DemoSnapshot Data => demo.Snapshot;
    public DemoDevice? Device => Data.Devices.FirstOrDefault(x => x.Id == DeviceId || x.Serial.Equals(DeviceId, StringComparison.OrdinalIgnoreCase));
}

public enum DeviceSelectionState
{
    NoSelection,
    AwaitingIntegration,
    InvalidRequest
}
