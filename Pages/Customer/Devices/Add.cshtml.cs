using System.ComponentModel.DataAnnotations;
using HydroSentinel.Backend;
using Microsoft.AspNetCore.Mvc;

namespace HydroSentinel.Pages.Customer.Devices;

/// <summary>Self-claim: the customer binds an unassigned unit with its serial and one-time device key.</summary>
public class AddModel(DeviceClaimService claims) : CustomerPageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Message { get; private set; }

    public IActionResult OnGet() => UserId is null ? SignInRequired() : Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();
        if (!ModelState.IsValid) return Page();

        var result = await claims.ClaimAsync(uid, Input.Serial, Input.Key, ct);
        switch (result.Outcome)
        {
            case ClaimOutcome.Invalid:
                ModelState.AddModelError(string.Empty, "Enter the serial number and the device key from the controller.");
                return Page();
            case ClaimOutcome.Rejected:
                ModelState.AddModelError(string.Empty, "That serial number and device key do not match.");
                return Page();
            case ClaimOutcome.Conflict:
                ModelState.AddModelError(string.Empty, "That unit is already registered to another account. Contact support if you believe this is wrong.");
                return Page();
            default:
                TempData["DeviceClaimed"] = result.Outcome == ClaimOutcome.AlreadyOwned
                    ? "That device is already on your account."
                    : $"Device {result.Device!.Name} added to your account.";
                return RedirectToPage("/Customer/Devices/Details", new { id = result.Device!.Id, claimed = 1 });
        }
    }

    public sealed class InputModel
    {
        [Required, StringLength(64, MinimumLength = 3)]
        [Display(Name = "Serial number")]
        public string Serial { get; set; } = "";

        [Required, StringLength(128, MinimumLength = 8)]
        [Display(Name = "Device key")]
        public string Key { get; set; } = "";
    }
}
