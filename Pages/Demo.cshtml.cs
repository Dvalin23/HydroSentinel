using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages;

public class DemoModel(IDemoMode demo) : PageModel
{
    public bool Reset { get; private set; }
    public IActionResult OnGet() => demo.Enabled ? Page() : NotFound();
    public IActionResult OnPostReset()
    {
        if (!demo.Enabled) return NotFound();
        demo.Reset();
        Reset = true;
        return Page();
    }
}
