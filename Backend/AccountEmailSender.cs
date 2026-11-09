using System.IO;
using System.Net.Sockets;
using HydroSentinel.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HydroSentinel.Backend;

public sealed class AccountEmailSender(IOptions<EmailOptions> options, ILogger<AccountEmailSender> logger)
    : IEmailSender<ApplicationUser>
{
    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
        => SendAsync(email, "Confirm your HydroSentinel email", user.FullName,
            "Confirm your email address to finish creating your HydroSentinel account.", confirmationLink);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
        => SendAsync(email, "Reset your HydroSentinel password", user.FullName,
            "Use the link below to reset your password. If you did not request this, you can ignore this email.", resetLink);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
        => SendAsync(email, "Your HydroSentinel password reset code", user.FullName,
            $"Use this code to reset your password: {resetCode}", null);

    public Task SendEmailChangeLinkAsync(ApplicationUser user, string email, string confirmationLink)
        => SendAsync(email, "Confirm your new HydroSentinel email", user.FullName,
            "Confirm this address to change the email on your HydroSentinel account.", confirmationLink);

    private async Task SendAsync(string email, string subject, string? name, string message, string? link)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Account email delivery is not configured.");

        var greeting = string.IsNullOrWhiteSpace(name) ? "there" : name;
        var encodedGreeting = System.Net.WebUtility.HtmlEncode(greeting);
        var encodedMessage = System.Net.WebUtility.HtmlEncode(message);
        var linkHtml = link is null ? string.Empty : $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Continue securely</a></p>";
        var plainLink = link is null ? string.Empty : $"\n\n{link}";

        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mail.To.Add(MailboxAddress.Parse(email));
        mail.Subject = subject;
        mail.Body = new BodyBuilder
        {
            HtmlBody = $"<p>Hello {encodedGreeting},</p><p>{encodedMessage}</p>{linkHtml}<p>If you did not request this, you can ignore this message.</p>",
            TextBody = $"Hello {greeting},\n\n{message}{plainLink}\n\nIf you did not request this, you can ignore this message."
        }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 15000 };
        try
        {
            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls);
            if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
                await client.AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword);
            await client.SendAsync(mail);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex) when (ex is SmtpCommandException or SmtpProtocolException or IOException
            or SocketException or TimeoutException or MailKit.Security.AuthenticationException
            or System.Security.Authentication.AuthenticationException)
        {
            // Do not log recipient addresses, confirmation/reset links, or token values.
            logger.LogError("SMTP delivery failed for account message {Subject} ({FailureType}).", subject, ex.GetType().Name);
            throw new AccountEmailDeliveryException("Account email delivery failed.", ex);
        }
    }
}

public sealed class AccountEmailDeliveryException(string message, Exception innerException)
    : Exception(message, innerException);

public sealed class AccountEmailLinks(
    IOptions<EmailOptions> options,
    IWebHostEnvironment environment,
    LinkGenerator linkGenerator)
{
    private readonly EmailOptions _options = options.Value;
    public bool IsConfigured => Uri.TryCreate(_options.PublicBaseUrl, UriKind.Absolute, out var origin)
        && origin.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(origin.UserInfo)
        && origin.AbsolutePath == "/"
        && string.IsNullOrEmpty(origin.Query)
        && string.IsNullOrEmpty(origin.Fragment)
        && (environment.IsDevelopment() || !origin.IsLoopback);

    public string Page(PageModel page, string pageName, object values)
    {
        if (!Uri.TryCreate(_options.PublicBaseUrl, UriKind.Absolute, out var origin)
            || !IsConfigured)
            throw new InvalidOperationException("Email:PublicBaseUrl must be a public HTTPS URL in production.");

        return linkGenerator.GetUriByPage(page.HttpContext, pageName, handler: null, values: values,
            scheme: origin.Scheme, host: new HostString(origin.Authority))
            ?? throw new InvalidOperationException("Could not build the account email link.");
    }
}
