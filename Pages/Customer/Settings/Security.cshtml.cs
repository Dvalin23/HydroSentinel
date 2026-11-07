using System.ComponentModel.DataAnnotations;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace HydroSentinel.Pages.Customer.Settings;
public class SecurityModel(IFormTestingMode formTestingMode) : PageModel
{
    public bool TestModeEnabled => formTestingMode.Enabled;
    public string? TestStatus { get; private set; }
    [BindProperty] public SecurityInput Input { get; set; } = new();
    public IActionResult OnPost()
    {
        if (!ModelState.IsValid || !TestModeEnabled) return Page();
        TestStatus = "Test submission successful. Password data was discarded; no password or session was changed.";
        Input = new();
        return Page();
    }
    public sealed class SecurityInput
    {
        [Required, DataType(DataType.Password), Display(Name="Current password")] public string? CurrentPassword { get; set; }
        [Required, MinLength(8), DataType(DataType.Password), Display(Name="New password")] public string? NewPassword { get; set; }
        [Required, Compare(nameof(NewPassword)), DataType(DataType.Password), Display(Name="Confirm new password")] public string? ConfirmPassword { get; set; }
    }
}
