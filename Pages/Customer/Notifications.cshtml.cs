using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Customer;

public class NotificationsModel(IDemoMode demo) : PageModel
{
    public IActionResult OnPostReadAll() { if (!demo.Enabled) return NotFound(); demo.MarkNotificationsRead(); return RedirectToPage(); }
}
