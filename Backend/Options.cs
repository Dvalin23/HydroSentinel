namespace HydroSentinel.Backend;

public sealed class PaystackOptions
{
    public const string Section = "Paystack";
    /// <summary>sk_test_... / sk_live_... Supply via user-secrets or environment, never appsettings.json.</summary>
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.paystack.co";
    /// <summary>Public origin used to build the callback URL, e.g. https://portal.example.com</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
    public Dictionary<string, string> PlanCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? PlanCodeFor(string planId)
        => PlanCodes.TryGetValue(planId, out var code) && !string.IsNullOrWhiteSpace(code) ? code.Trim() : null;
}

public sealed class DeviceOptions
{
    public const string Section = "Device";
    /// <summary>Optional shared secret for a trusted relay using X-Relay-Secret. Direct devices must use HTTPS and their per-device key.</summary>
    public string RelaySecret { get; set; } = string.Empty;
}

public sealed class FeatureOptions
{
    public const string Section = "Features";
    /// <summary>
    /// Master switch for anything that can stop a pump remotely: admin lock/unlock AND
    /// subscription-expiry lock. The repo checklist marks remote commands "requires confirmation"
    /// and safety interlocks "blocked", so this ships OFF. While off, check-in always returns lock=0.
    /// </summary>
    public bool RemoteLockEnabled { get; set; }
}

public sealed class SeedOptions
{
    public const string Section = "Seed";
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}

public sealed class EmailOptions
{
    public const string Section = "Email";
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "HydroSentinel";
    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost)
        && !string.IsNullOrWhiteSpace(FromAddress)
        && SmtpPort is >= 1 and <= 65535
        && EnableSsl
        && (string.IsNullOrEmpty(SmtpUsername) == string.IsNullOrEmpty(SmtpPassword))
        && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
