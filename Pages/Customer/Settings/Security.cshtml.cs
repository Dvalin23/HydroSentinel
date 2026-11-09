using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HydroSentinel.Backend;
using HydroSentinel.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace HydroSentinel.Pages.Customer.Settings;

[EnableRateLimiting("auth")]
public sealed class SecurityModel(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    AuditService audit) : PageModel
{
    [BindProperty] public SecurityInput Input { get; set; } = new();
    public string? StatusMessage { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await users.FindByIdAsync(userId);
        if (user is null) return RedirectToPage("/Login", new { ReturnUrl = Request.Path });

        var result = await users.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var message = error.Code == "PasswordMismatch"
                    ? "The current password is incorrect."
                    : error.Description;
                ModelState.AddModelError(nameof(Input.CurrentPassword), message);
            }
            Input = new();
            return Page();
        }

        await signIn.RefreshSignInAsync(user);
        await audit.WriteAsync(user.Id, "account.password.changed", user.Id, null);
        Input = new();
        StatusMessage = "Your password was changed.";
        return Page();
    }

    public sealed class SecurityInput
    {
        [Required, DataType(DataType.Password), StringLength(128), Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, MinLength(10), StringLength(128), DataType(DataType.Password), Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, Compare(nameof(NewPassword)), DataType(DataType.Password), Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
