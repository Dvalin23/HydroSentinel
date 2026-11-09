using System.ComponentModel.DataAnnotations;
using System.Text;
using HydroSentinel.Data;
using HydroSentinel.Backend;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace HydroSentinel.Pages;

[EnableRateLimiting("auth")]
public class GetStartedModel(
    UserManager<ApplicationUser> users,
    AccountEmailSender emailSender,
    AccountEmailLinks emailLinks,
    AuditService audit,
    ILogger<GetStartedModel> logger) : PageModel
{
    [BindProperty] public RegisterInput Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        if (!emailSender.IsConfigured || !emailLinks.IsConfigured)
        {
            ModelState.AddModelError(string.Empty, "Account email delivery is not configured yet. Please contact support to create an account.");
            return Page();
        }

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
                ModelState.AddModelError(string.Empty, e.Code.StartsWith("Duplicate")
                    ? "We could not create an account with those details. If you already registered, try signing in or resetting your password."
                    : e.Description);
            }
            return Page();
        }
        var roleResult = await users.AddToRoleAsync(user, Roles.Customer);
        if (!roleResult.Succeeded)
        {
            await users.DeleteAsync(user);
            logger.LogError("New customer role assignment failed: {Errors}", string.Join(",", roleResult.Errors.Select(e => e.Code)));
            ModelState.AddModelError(string.Empty, "We could not finish creating the account. Please try again.");
            return Page();
        }

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var confirmationLink = emailLinks.Page(this, "/Account/ConfirmEmail", new { userId = user.Id, code });
        try
        {
            await emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink);
        }
        catch (AccountEmailDeliveryException)
        {
            await audit.WriteAsync(user.Id, "account.registration.created", user.Id, "confirmation_email_failed");
            return RedirectToPage("/Account/RegisterConfirmation", new { deliveryFailed = true });
        }

        await audit.WriteAsync(user.Id, "account.registration.created", user.Id, "confirmation_sent");
        return RedirectToPage("/Account/RegisterConfirmation");
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
