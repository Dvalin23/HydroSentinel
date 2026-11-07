using System.ComponentModel.DataAnnotations;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Customer.Support;

public class CreateModel(IFormTestingMode formTestingMode, IDemoMode demo) : PageModel
{
    public bool TestModeEnabled => formTestingMode.Enabled;
    public string? TestStatus { get; private set; }
    [BindProperty]
    public TicketInput Input { get; set; } = new();

    public void OnGet() { }
    public IActionResult OnPost()
    {
        if (!ModelState.IsValid || !TestModeEnabled)
        {
            return Page();
        }

        if (TestModeEnabled) { var ticket = demo.AddTicket(Input.Subject!, Input.Category!, Input.DeviceId, Input.Description!); TestStatus = $"DEMO MODE: ticket {ticket.Reference} created temporarily. No email was sent and no ticket was persisted."; }
        Input = new();
        return Page();
    }

    public sealed class TicketInput
    {
        [Required, StringLength(160)]
        [Display(Name = "Subject")]
        public string? Subject { get; set; }
        [Required, StringLength(80)]
        [Display(Name = "Category")]
        public string? Category { get; set; }
        [StringLength(160)]
        public string? DeviceId { get; set; }
        [Required, StringLength(4000, MinimumLength = 10)]
        [Display(Name = "Description")]
        public string? Description { get; set; }
    }
}
