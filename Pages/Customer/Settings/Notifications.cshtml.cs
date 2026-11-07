using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HydroSentinel.Services;
namespace HydroSentinel.Pages.Customer.Settings;
public class NotificationPreferencesModel(IFormTestingMode formTestingMode, IDemoMode demo) : PageModel
{
    public bool TestModeEnabled => formTestingMode.Enabled;
    public string? TestStatus { get; private set; }
    [BindProperty] public PreferencesInput Input { get; set; } = new();
    public void OnGet() { if (TestModeEnabled) { var p = demo.Snapshot.Preferences; Input = new() { DeviceAlerts = p.DeviceAlerts, BillingUpdates = p.BillingUpdates, SupportUpdates = p.SupportUpdates }; } }
    public IActionResult OnPost()
    {
        if (!ModelState.IsValid || !TestModeEnabled) return Page();
        demo.UpdatePreferences(Input.BillingUpdates, Input.SupportUpdates);
        TestStatus = "DEMO MODE: notification preferences updated temporarily. Nothing was sent or persisted.";
        return Page();
    }
    public sealed class PreferencesInput
    {
        public bool DeviceAlerts { get; set; }
        public bool BillingUpdates { get; set; }
        public bool SupportUpdates { get; set; }
    }
}
