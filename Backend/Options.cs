namespace HydroSentinel.Backend;

public sealed class PaystackOptions
{
    public const string Section = "Paystack";
    /// <summary>sk_test_... / sk_live_... Supply via user-secrets or environment, never appsettings.json.</summary>
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.paystack.co";
    /// <summary>Public origin used to build the callback URL, e.g. https://portal.example.com</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}

public sealed class DeviceOptions
{
    public const string Section = "Device";
    /// <summary>Shared secret the PHP/HTTP relay sends in X-Relay-Secret. Required.</summary>
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
