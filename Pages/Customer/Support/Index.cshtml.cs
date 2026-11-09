using System.Security.Claims;
using HydroSentinel.Backend;
using HydroSentinel.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Customer.Support;

public class IndexModel(SupportTicketService tickets) : PageModel
{
    public IReadOnlyList<SupportTicket> Tickets { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId)) Tickets = await tickets.CustomerTicketsAsync(userId, ct);
    }
}
