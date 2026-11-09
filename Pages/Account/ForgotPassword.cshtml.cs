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
public sealed class ForgotPasswordModel(
    UserManager<ApplicationUser> users,
    AccountEmailSender emailSender,
    AccountEmailLinks emailLinks,
    ILogger<ForgotPasswordModel> logger) : PageModel
{
    [BindProperty] public ForgotPasswordInput Input { get; set; } = new();
    public string? StatusMessage { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        if (!emailSender.IsConfigured || !emailLinks.IsConfigured)
        {
            StatusMessage = "Password recovery is not configured. Please contact support.";
            return Page();
        }

        try
        {
            var user = await users.FindByEmailAsync(Input.Email.Trim());
            if (user is not null && user.EmailConfirmed)
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var link = emailLinks.Page(this, "/Account/ResetPassword", new { userId = user.Id, code });
                await emailSender.SendPasswordResetLinkAsync(user, user.Email!, link);
            }
            StatusMessage = "If a confirmed account uses that address, password reset instructions have been sent.";
        }
        catch (AccountEmailDeliveryException)
        {
            logger.LogWarning("Password reset email could not be delivered.");
            StatusMessage = "We could not send the email right now. Please try again shortly.";
        }

        return Page();
    }

    public sealed class ForgotPasswordInput
    {
        [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    }
}
