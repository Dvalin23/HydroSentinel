using HydroSentinel.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using HydroSentinel.Data;

namespace HydroSentinel.Pages.Admin;

public class AdminPageModel(IAdminPortalProjection portal, IDemoMode demo, SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users) : PageModel
{
    public IAdminPortalProjection Portal { get; } = portal;
    public AdminPortalSnapshot Data => Portal.Snapshot;
    public bool IsDemo => Portal.IsDevelopmentDemo;
    [BindProperty(SupportsGet = true)] public string? Id { get; set; }
    [BindProperty] public AdminLoginInput Input { get; set; } = new();
    public void OnGet() { }

    public async Task<IActionResult> OnPostSignInAsync()
    {
        if (!ModelState.IsValid) return Page();

        // Real sign-in always runs. Only accounts in the Admin role are let through.
        var user = await users.FindByEmailAsync(Input.Email.Trim());
        const string generic = "Email or password is incorrect.";
        if (user is null || !await users.IsInRoleAsync(user, Roles.Admin))
        {
            ModelState.AddModelError(string.Empty, generic);
            return Page();
        }
        var result = await signIn.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.IsLockedOut ? "Too many attempts. Try again in a few minutes." : generic);
            return Page();
        }
        return RedirectToPage("/Admin/Dashboard");
    }

    public IActionResult OnPostMarkNotificationsRead()
    {
        if (!IsDemo) return NotFound();
        demo.MarkNotificationsRead();
        return RedirectToPage("/Admin/Notifications");
    }
}

public sealed class AdminLoginInput
{
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
