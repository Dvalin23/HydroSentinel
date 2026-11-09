using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Security.Claims;
using HydroSentinel.Backend;
using HydroSentinel.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace HydroSentinel.Pages.Customer.Settings;

[EnableRateLimiting("auth")]
public sealed class ProfileModel(
    UserManager<ApplicationUser> users,
    AccountEmailSender emailSender,
    AccountEmailLinks emailLinks,
    AuditService audit) : PageModel
{
    [BindProperty] public ProfileInput Input { get; set; } = new();
    public string? CurrentEmail { get; private set; }
    public string? StatusMessage { get; private set; }
    public bool StatusIsError { get; private set; }
    public bool EmailDeliveryConfigured => emailSender.IsConfigured && emailLinks.IsConfigured;

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await CurrentUserAsync();
        if (user is null) return RedirectToPage("/Login", new { ReturnUrl = Request.Path });
        CurrentEmail = user.Email;
        Input = new() { FullName = user.FullName, Email = user.Email ?? string.Empty, Phone = user.Phone };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await CurrentUserAsync();
        if (user is null) return RedirectToPage("/Login", new { ReturnUrl = Request.Path });

        var requestedEmail = Input.Email.Trim();
        var emailChanged = !string.Equals(requestedEmail, user.Email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged && !EmailDeliveryConfigured)
            ModelState.AddModelError(nameof(Input.Email), "Email delivery is unavailable, so the new address cannot be verified yet.");
        if (emailChanged && EmailDeliveryConfigured)
        {
            var emailOwner = await users.FindByEmailAsync(requestedEmail);
            var usernameOwner = await users.FindByNameAsync(requestedEmail);
            if ((emailOwner is not null && emailOwner.Id != user.Id)
                || (usernameOwner is not null && usernameOwner.Id != user.Id))
                ModelState.AddModelError(nameof(Input.Email), "That email address is unavailable.");
        }
        if (!ModelState.IsValid)
        {
            CurrentEmail = user.Email;
            return Page();
        }

        user.FullName = Input.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(Input.Phone) ? null : Input.Phone.Trim();
        var update = await users.UpdateAsync(user);
        if (!update.Succeeded)
        {
            AddIdentityErrors(update);
            CurrentEmail = user.Email;
            return Page();
        }

        await audit.WriteAsync(user.Id, "account.profile.updated", user.Id, "name_or_phone");
        StatusMessage = "Your profile was saved.";

        if (emailChanged)
        {
            var token = await users.GenerateChangeEmailTokenAsync(user, requestedEmail);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var link = emailLinks.Page(this, "/Account/ConfirmEmail", new { userId = user.Id, newEmail = requestedEmail, code });
            try
            {
                await emailSender.SendEmailChangeLinkAsync(user, requestedEmail, link);
                await audit.WriteAsync(user.Id, "account.email.change_requested", user.Id, "verification_sent");
                StatusMessage += " The new address is pending until you confirm the link sent to it.";
            }
            catch (AccountEmailDeliveryException)
            {
                StatusIsError = true;
                StatusMessage += " We could not send the email confirmation. Your current address is unchanged; save again to retry.";
            }
        }

        CurrentEmail = user.Email;
        return Page();
    }

    private Task<ApplicationUser?> CurrentUserAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is null ? Task.FromResult<ApplicationUser?>(null) : users.FindByIdAsync(id);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }

    public sealed class ProfileInput
    {
        [Required, StringLength(120, MinimumLength = 2), Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256), Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Phone, StringLength(32), Display(Name = "Phone number")]
        public string? Phone { get; set; }
    }
}
