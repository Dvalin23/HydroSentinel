using System.Text;
using HydroSentinel.Backend;
using HydroSentinel.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace HydroSentinel.Pages.Account;

[EnableRateLimiting("auth")]
public sealed class ConfirmEmailModel(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    AuditService audit,
    ILogger<ConfirmEmailModel> logger) : PageModel
{
    public string Message { get; private set; } = "This confirmation link is invalid or has expired.";
    public bool Succeeded { get; private set; }
    public bool IsEmailChange { get; private set; }

    public async Task OnGetAsync(string? userId, string? code, string? newEmail)
    {
        IsEmailChange = !string.IsNullOrWhiteSpace(newEmail);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code)) return;

        var user = await users.FindByIdAsync(userId);
        if (user is null) return;

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return;
        }

        var previousEmail = user.Email;
        var result = IsEmailChange
            ? await users.ChangeEmailAsync(user, newEmail!, token)
            : await users.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            logger.LogInformation("Account email confirmation failed for user {UserId}.", user.Id);
            return;
        }

        if (IsEmailChange && string.Equals(user.UserName, previousEmail, StringComparison.OrdinalIgnoreCase))
        {
            var userNameResult = await users.SetUserNameAsync(user, newEmail!);
            if (!userNameResult.Succeeded)
                logger.LogError("Email changed but the sign-in name could not be synchronized for user {UserId}.", user.Id);
        }

        Succeeded = true;
        Message = IsEmailChange ? "Your new email address is confirmed." : "Your email address is confirmed. You can now sign in.";
        await audit.WriteAsync(user.Id, IsEmailChange ? "account.email.changed" : "account.email.confirmed", user.Id, null);

        if (User.Identity?.IsAuthenticated == true && User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == user.Id)
            await signIn.RefreshSignInAsync(user);
    }
}
