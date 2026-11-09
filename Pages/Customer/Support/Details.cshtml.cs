using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HydroSentinel.Backend;
using HydroSentinel.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Pages.Customer.Support;

[EnableRateLimiting("auth")]
public class DetailsModel(SupportTicketService tickets, AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "id")]
    public string? Id { get; set; }

    public SupportTicket Ticket { get; private set; } = null!;
    public string? DeviceName { get; private set; }
    [BindProperty]
    public ReplyInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostReplyAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 32) return NotFound();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        if (!ModelState.IsValid) return await LoadAsync(ct);

        if (!await tickets.CustomerReplyAsync(userId, Id, Input.Message!, ct)) return NotFound();
        TempData["SupportStatus"] = "Your reply was added to the request.";
        return RedirectToPage(new { id = Id });
    }

    private async Task<IActionResult> LoadAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 32) return NotFound();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();
        var ticket = await tickets.CustomerTicketAsync(userId, Id, ct);
        if (ticket is null) return NotFound();
        Ticket = ticket;
        DeviceName = ticket.DeviceId is null ? null : await db.Devices.AsNoTracking()
            .Where(d => d.Id == ticket.DeviceId && d.OwnerId == userId).Select(d => d.Name).FirstOrDefaultAsync(ct);
        return Page();
    }

    public sealed class ReplyInput
    {
        [Required, StringLength(4000, MinimumLength = 2)]
        [Display(Name = "Your reply")]
        public string? Message { get; set; }
    }
}
