using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace HydroSentinel.Backend;

public sealed record PaystackInit(bool Ok, string? AuthorizationUrl, string? Reference, string? Error);
public sealed record PaystackVerify(bool Reachable, string? Status, long AmountMinor, string? Currency, string? Reference, string? Channel, string? CustomerCode, string? AuthorizationCode, bool Reusable, string? CardBrand, string? CardLast4, string? Error);
public sealed record PaystackPlan(bool Ok, string? Error);
public sealed record PaystackSubscription(bool Ok, string? SubscriptionCode, string? Error);
public sealed record PaystackRefund(bool Ok, string? RefundReference, string? Status, string? Error);

public sealed class PaystackClient(HttpClient http, IOptions<PaystackOptions> options, IWebHostEnvironment environment)
{
    private readonly PaystackOptions opt = options.Value;

    // Prevent accidentally charging real cards from a local Development run.
    public bool Configured => environment.IsDevelopment()
        ? opt.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal)
        : environment.IsProduction()
            ? opt.SecretKey.StartsWith("sk_live_", StringComparison.Ordinal)
            : opt.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal)
                || opt.SecretKey.StartsWith("sk_live_", StringComparison.Ordinal);

    public bool UsesTestKey => opt.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal);

    private HttpRequestMessage Req(HttpMethod method, string path)
    {
        var r = new HttpRequestMessage(method, opt.BaseUrl.TrimEnd('/') + path);
        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.SecretKey);
        return r;
    }

    public async Task<PaystackInit> InitializeAsync(string email, long amountMinor, string currency, string reference, string callbackUrl, CancellationToken ct, bool cardOnly = false)
    {
        if (!Configured) return new PaystackInit(false, null, null, "payments_not_configured");
        try
        {
            var req = Req(HttpMethod.Post, "/transaction/initialize");
            req.Content = JsonContent.Create(new
            {
                email,
                amount = amountMinor,
                currency,
                reference,
                callback_url = callbackUrl,
                channels = cardOnly ? new[] { "card" } : null
            });
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadFromJsonAsync<InitEnvelope>(cancellationToken: ct);
            if (!res.IsSuccessStatusCode || body is null || !body.Status || body.Data is null)
                return new PaystackInit(false, null, null, "provider_rejected");
            return new PaystackInit(true, body.Data.AuthorizationUrl, body.Data.Reference ?? reference, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new PaystackInit(false, null, null, "provider_unavailable");
        }
    }

    public async Task<PaystackVerify> VerifyAsync(string reference, CancellationToken ct)
    {
        if (!Configured) return new PaystackVerify(false, null, 0, null, null, null, null, null, false, null, null, "payments_not_configured");
        try
        {
            using var req = Req(HttpMethod.Get, "/transaction/verify/" + Uri.EscapeDataString(reference));
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadFromJsonAsync<VerifyEnvelope>(cancellationToken: ct);
            if (!res.IsSuccessStatusCode || body is null || !body.Status || body.Data is null)
                return new PaystackVerify(false, null, 0, null, null, null, null, null, false, null, null, "unable_to_verify");
            return new PaystackVerify(true, body.Data.Status, body.Data.Amount, body.Data.Currency, body.Data.Reference, body.Data.Channel,
                body.Data.Customer?.CustomerCode, body.Data.Authorization?.AuthorizationCode, body.Data.Authorization?.Reusable ?? false,
                body.Data.Authorization?.Brand, body.Data.Authorization?.Last4, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new PaystackVerify(false, null, 0, null, null, null, null, null, false, null, null, "provider_unavailable");
        }
    }

    public async Task<PaystackPlan> CheckMonthlyPlanAsync(string code, long amountMinor, CancellationToken ct)
    {
        if (!Configured) return new(false, "payments_not_configured");
        try
        {
            using var req = Req(HttpMethod.Get, "/plan/" + Uri.EscapeDataString(code));
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadFromJsonAsync<PlanEnvelope>(cancellationToken: ct);
            return res.IsSuccessStatusCode && body is { Status: true, Data: not null }
                && body.Data.Interval == "monthly" && body.Data.Amount == amountMinor && body.Data.Currency == "NGN"
                ? new(true, null) : new(false, "plan_configuration_mismatch");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { return new(false, "provider_unavailable"); }
    }

    public async Task<PaystackSubscription> CreateSubscriptionAsync(string customerCode, string authorizationCode, string planCode, DateTimeOffset startAt, CancellationToken ct)
    {
        if (!Configured) return new(false, null, "payments_not_configured");
        try
        {
            using var req = Req(HttpMethod.Post, "/subscription");
            req.Content = JsonContent.Create(new { customer = customerCode, authorization = authorizationCode, plan = planCode, start_date = startAt.ToUniversalTime().ToString("O") });
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadFromJsonAsync<SubscriptionEnvelope>(cancellationToken: ct);
            if (!res.IsSuccessStatusCode || body is not { Status: true, Data: not null } || string.IsNullOrWhiteSpace(body.Data.SubscriptionCode))
                return new(false, null, "subscription_rejected");
            return new(true, body.Data.SubscriptionCode, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { return new(false, null, "provider_unavailable"); }
    }

    public async Task<PaystackRefund> RefundAsync(string reference, long amountMinor, CancellationToken ct)
    {
        if (!Configured) return new(false, null, null, "payments_not_configured");
        try
        {
            using var req = Req(HttpMethod.Post, "/refund");
            req.Content = JsonContent.Create(new { transaction = reference, amount = amountMinor, currency = "NGN" });
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadFromJsonAsync<RefundEnvelope>(cancellationToken: ct);
            if (!res.IsSuccessStatusCode || body is not { Status: true }) return new(false, null, null, "refund_rejected");
            return new(true, body.Data?.Id.ToString(), body.Data?.Status, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { return new(false, null, null, "provider_unavailable"); }
    }

    public async Task<string?> SubscriptionManagementLinkAsync(string subscriptionCode, CancellationToken ct)
    {
        if (!Configured) return null;
        try
        {
            using var req = Req(HttpMethod.Get, "/subscription/" + Uri.EscapeDataString(subscriptionCode) + "/manage/link");
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadFromJsonAsync<ManageEnvelope>(cancellationToken: ct);
            return res.IsSuccessStatusCode && body is { Status: true, Data.Link: not null }
                && Uri.TryCreate(body.Data.Link, UriKind.Absolute, out var uri) && uri.Scheme == "https" ? uri.ToString() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { return null; }
    }

    /// <summary>x-paystack-signature = lowercase hex HMAC-SHA512 of the raw request body, keyed with the secret key.</summary>
    public bool SignatureValid(byte[] rawBody, string? signatureHeader)
    {
        if (!Configured || string.IsNullOrWhiteSpace(signatureHeader)) return false;
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(opt.SecretKey));
        var expected = Convert.ToHexString(hmac.ComputeHash(rawBody)).ToLowerInvariant();
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(signatureHeader.Trim().ToLowerInvariant());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private sealed class InitEnvelope
    {
        [JsonPropertyName("status")] public bool Status { get; set; }
        [JsonPropertyName("data")] public InitData? Data { get; set; }
    }
    private sealed class InitData
    {
        [JsonPropertyName("authorization_url")] public string? AuthorizationUrl { get; set; }
        [JsonPropertyName("reference")] public string? Reference { get; set; }
    }
    private sealed class VerifyEnvelope
    {
        [JsonPropertyName("status")] public bool Status { get; set; }
        [JsonPropertyName("data")] public VerifyData? Data { get; set; }
    }
    private sealed class VerifyData
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("amount")] public long Amount { get; set; }
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("reference")] public string? Reference { get; set; }
        [JsonPropertyName("channel")] public string? Channel { get; set; }
        [JsonPropertyName("customer")] public VerifyCustomer? Customer { get; set; }
        [JsonPropertyName("authorization")] public VerifyAuthorization? Authorization { get; set; }
    }
    private sealed class VerifyCustomer { [JsonPropertyName("customer_code")] public string? CustomerCode { get; set; } }
    private sealed class VerifyAuthorization
    {
        [JsonPropertyName("authorization_code")] public string? AuthorizationCode { get; set; }
        [JsonPropertyName("reusable")] public bool Reusable { get; set; }
        [JsonPropertyName("brand")] public string? Brand { get; set; }
        [JsonPropertyName("last4")] public string? Last4 { get; set; }
    }
    private sealed class PlanEnvelope { [JsonPropertyName("status")] public bool Status { get; set; } [JsonPropertyName("data")] public PlanData? Data { get; set; } }
    private sealed class PlanData { [JsonPropertyName("interval")] public string? Interval { get; set; } [JsonPropertyName("amount")] public long Amount { get; set; } [JsonPropertyName("currency")] public string? Currency { get; set; } }
    private sealed class SubscriptionEnvelope { [JsonPropertyName("status")] public bool Status { get; set; } [JsonPropertyName("data")] public SubscriptionData? Data { get; set; } }
    private sealed class SubscriptionData { [JsonPropertyName("subscription_code")] public string? SubscriptionCode { get; set; } }
    private sealed class RefundEnvelope { [JsonPropertyName("status")] public bool Status { get; set; } [JsonPropertyName("data")] public RefundData? Data { get; set; } }
    private sealed class RefundData { [JsonPropertyName("id")] public long Id { get; set; } [JsonPropertyName("status")] public string? Status { get; set; } }
    private sealed class ManageEnvelope { [JsonPropertyName("status")] public bool Status { get; set; } [JsonPropertyName("data")] public ManageData? Data { get; set; } }
    private sealed class ManageData { [JsonPropertyName("link")] public string? Link { get; set; } }
}
