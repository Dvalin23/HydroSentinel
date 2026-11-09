using System.ComponentModel.DataAnnotations;
using System.Text;
using HydroSentinel.Backend;
using HydroSentinel.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace HydroSentinel.Pages.Account;

[EnableRateLimiting("auth")]
public sealed class ResetPasswordModel(UserManager<ApplicationUser> users, AuditService audit) : PageModel
{
    [BindProperty] public ResetPasswordInput Input { get; set; } = new();
    public string? StatusMessage { get; private set; }

    public IActionResult OnGet(string? userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            return RedirectToPage("/Account/ForgotPassword");

        Input.UserId = userId;
        Input.Code = code;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await users.FindByIdAsync(Input.UserId);
        if (user is null || string.IsNullOrWhiteSpace(Input.Code))
        {
            StatusMessage = "This reset link is invalid or has expired. Request a new one.";
            return Page();
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.Code));
        }
        catch (FormatException)
        {
            StatusMessage = "This reset link is invalid or has expired. Request a new one.";
            return Page();
        }

        var result = await users.ResetPasswordAsync(user, token, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(nameof(Input.Password), error.Description);
            return Page();
        }

        await audit.WriteAsync(user.Id, "account.password.reset", user.Id, null);
        TempData["StatusMessage"] = "Your password has been reset. Sign in with your new password.";
        return RedirectToPage("/Login");
    }

    public sealed class ResetPasswordInput
    {
        [Required, StringLength(450)] public string UserId { get; set; } = string.Empty;
        [Required] public string Code { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 10)] public string Password { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
    }
}
