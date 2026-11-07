using System.ComponentModel.DataAnnotations;
using HydroSentinel.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace HydroSentinel.Pages;

[EnableRateLimiting("auth")]
public class LoginModel(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await users.FindByEmailAsync(Input.Email.Trim());
        // Same message for unknown user, wrong password and lockout: do not reveal which accounts exist.
        const string generic = "Email or password is incorrect.";
        if (user is null) { ModelState.AddModelError(string.Empty, generic); return Page(); }

        var result = await signIn.PasswordSignInAsync(user, Input.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.IsLockedOut ? "Too many attempts. Try again in a few minutes." : generic); return Page(); }

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)) return LocalRedirect(ReturnUrl);
        return await users.IsInRoleAsync(user, Roles.Admin) ? RedirectToPage("/Admin/Dashboard") : RedirectToPage("/Customer/Index");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await signIn.SignOutAsync();
        return RedirectToPage("/Index");
    }
}

public sealed class LoginInput
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(128)] public string Password { get; set; } = string.Empty;
}
