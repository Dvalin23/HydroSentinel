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
public sealed class ResendConfirmationModel(
    UserManager<ApplicationUser> users,
    AccountEmailSender emailSender,
    AccountEmailLinks emailLinks) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Email { get; set; }
    public string? StatusMessage { get; private set; }
    public bool DeliveryConfigured => emailSender.IsConfigured && emailLinks.IsConfigured;

    public async Task<IActionResult> OnPostAsync(string? email)
    {
        Email = email?.Trim();
        if (string.IsNullOrWhiteSpace(Email) || !new EmailAddressAttribute().IsValid(Email))
        {
            ModelState.AddModelError(nameof(Email), "Enter a valid email address.");
            return Page();
        }

        if (!emailSender.IsConfigured || !emailLinks.IsConfigured)
        {
            StatusMessage = "Email delivery is not configured. Please contact support.";
            return Page();
        }

        try
        {
            var user = await users.FindByEmailAsync(Email);
            if (user is not null && !user.EmailConfirmed)
            {
                var token = await users.GenerateEmailConfirmationTokenAsync(user);
                var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var link = emailLinks.Page(this, "/Account/ConfirmEmail", new { userId = user.Id, code });
                await emailSender.SendConfirmationLinkAsync(user, user.Email!, link);
            }
            StatusMessage = "If the address belongs to an account that needs confirmation, a new link has been sent.";
        }
        catch (AccountEmailDeliveryException)
        {
            StatusMessage = "We could not send the email right now. Please try again shortly.";
        }

        return Page();
    }
}
