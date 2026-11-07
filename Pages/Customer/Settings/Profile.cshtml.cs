using System.ComponentModel.DataAnnotations;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace HydroSentinel.Pages.Customer.Settings;
public class ProfileModel(IFormTestingMode formTestingMode, IDemoMode demo) : PageModel
{
    public bool TestModeEnabled => formTestingMode.Enabled;
    public string? TestStatus { get; private set; }
    [BindProperty] public ProfileInput Input { get; set; } = new();
    public void OnGet() { if (TestModeEnabled) { var p = demo.Snapshot.Profile; Input = new() { FullName = p.FullName, Email = p.Email, Phone = p.Phone, Company = p.Company }; } }
    public IActionResult OnPost()
    {
        if (!ModelState.IsValid || !TestModeEnabled) return Page();
        demo.UpdateProfile(Input.FullName ?? "", Input.Email ?? "", Input.Phone ?? "", Input.Company ?? "");
        TestStatus = "DEMO MODE: profile updated temporarily. No customer record was saved.";
        return Page();
    }
    public sealed class ProfileInput
    {
        [StringLength(160), Display(Name="Full name")] public string? FullName { get; set; }
        [EmailAddress, StringLength(254), Display(Name="Email address")] public string? Email { get; set; }
        [Phone, StringLength(40), Display(Name="Phone number")] public string? Phone { get; set; }
        [StringLength(160), Display(Name="Company or installation name")] public string? Company { get; set; }
    }
}
