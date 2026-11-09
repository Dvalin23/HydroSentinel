using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HydroSentinel.Backend;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace HydroSentinel.Pages.Customer.Support;

[EnableRateLimiting("auth")]
public class CreateModel(SupportTicketService tickets, CustomerPortal customerPortal) : PageModel
{
    public IReadOnlyList<CustomerDeviceVm> Devices { get; private set; } = [];

    [BindProperty]
    public TicketInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct) => await LoadDevicesAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        await LoadDevicesAsync(ct);
        if (!ModelState.IsValid) return Page();

        try
        {
            var ticket = await tickets.CreateAsync(userId, Input.Subject!, Input.Category!, Input.DeviceId, Input.Description!, ct);
            return RedirectToPage("/Customer/Support/Details", new { id = ticket.Reference });
        }
        catch (UnownedSupportDeviceException)
        {
            ModelState.AddModelError("Input.DeviceId", "Select a device from your account or leave this field empty.");
            return Page();
        }
    }

    private async Task LoadDevicesAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Devices = string.IsNullOrEmpty(userId) ? [] : await customerPortal.DevicesAsync(userId, ct);
    }

    public sealed class TicketInput
    {
        [Required, StringLength(160, MinimumLength = 3)]
        [Display(Name = "Subject")]
        public string? Subject { get; set; }

        [Required, StringLength(80), RegularExpression("^(Account access|Device support|Monitoring|Billing|Other)$")]
        [Display(Name = "Category")]
        public string? Category { get; set; }

        [Display(Name = "Related device")]
        public Guid? DeviceId { get; set; }

        [Required, StringLength(4000, MinimumLength = 10)]
        [Display(Name = "Description")]
        public string? Description { get; set; }
    }
}
