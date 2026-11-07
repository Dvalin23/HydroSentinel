using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using HydroSentinel.Services;

namespace HydroSentinel.Pages.Customer.Support;

public class DetailsModel(IDemoMode demo) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "id")]
    public string? Id { get; set; }
    public bool HasId => !string.IsNullOrWhiteSpace(Id) && !HasInvalidId;
    public bool HasInvalidId => !string.IsNullOrWhiteSpace(Id) && (Id!.Length > 128 || Id.Any(char.IsControl));
    public DemoTicket? Ticket => demo.Snapshot.Tickets.FirstOrDefault(x => x.Reference.Equals(Id, StringComparison.OrdinalIgnoreCase));
}
