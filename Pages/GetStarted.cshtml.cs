using System.ComponentModel.DataAnnotations;
using HydroSentinel.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace HydroSentinel.Pages;

[EnableRateLimiting("auth")]
public class GetStartedModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : PageModel
{
    [BindProperty] public RegisterInput Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        // Customers can only ever be created here with the Customer role. Admin is never self-service.
        var user = new ApplicationUser
        {
            UserName = Input.Email.Trim(),
            Email = Input.Email.Trim(),
            FullName = Input.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim()
        };
        var result = await users.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
            {
                // Duplicate email: say so generically enough to avoid easy enumeration of other fields.
                ModelState.AddModelError(string.Empty, e.Code.StartsWith("Duplicate") ? "An account with that email already exists. Try signing in." : e.Description);
            }
            return Page();
        }
        await users.AddToRoleAsync(user, Roles.Customer);
        await signIn.SignInAsync(user, isPersistent: false);
        return RedirectToPage("/Customer/Index");
    }
}

public sealed class RegisterInput
{
    [Required, StringLength(120, MinimumLength = 2)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Phone, StringLength(32)] public string? Phone { get; set; }
    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 10)] public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")] public string ConfirmPassword { get; set; } = string.Empty;
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms to continue.")] public bool AcceptTerms { get; set; }
}
