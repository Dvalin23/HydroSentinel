using System.ComponentModel.DataAnnotations;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Customer.Devices;

public class AddModel(IDemoMode demo) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Message { get; private set; }
    public IActionResult OnPost()
    {
        if (!demo.Enabled) return NotFound();
        if (!ModelState.IsValid) return Page();
        var device = demo.AddDevice(Input.Serial, Input.Name, Input.Location);
        Message = device is null ? "That serial number is already in the demo account." : $"Demo device {device.Name} added. No ownership verification or hardware communication occurred.";
        if (device is not null) Input = new();
        return Page();
    }
    public sealed class InputModel
    {
        [Required, StringLength(40), RegularExpression("HS-[A-Za-z0-9-]+", ErrorMessage = "Use a demo identifier such as HS-009999.")] public string Serial { get; set; } = "";
        [Required, StringLength(100)] public string Name { get; set; } = "";
        [Required, StringLength(120)] public string Location { get; set; } = "";
    }
}
