using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace HydroSentinel.Backend;

public sealed record PaystackInit(bool Ok, string? AuthorizationUrl, string? Reference, string? Error);
public sealed record PaystackVerify(bool Reachable, string? Status, long AmountMinor, string? Currency, string? Reference, string? Channel, string? Error);

public sealed class PaystackClient(HttpClient http, IOptions<PaystackOptions> options)
{
    private readonly PaystackOptions opt = options.Value;

    public bool Configured => !string.IsNullOrWhiteSpace(opt.SecretKey);

    private HttpRequestMessage Req(HttpMethod method, string path)
    {
        var r = new HttpRequestMessage(method, opt.BaseUrl.TrimEnd('/') + path);
        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.SecretKey);
        return r;
    }

    public async Task<PaystackInit> InitializeAsync(string email, long amountMinor, string currency, string reference, string callbackUrl, CancellationToken ct)
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
                callback_url = callbackUrl
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
        if (!Configured) return new PaystackVerify(false, null, 0, null, null, null, "payments_not_configured");
        try
        {
            using var req = Req(HttpMethod.Get, "/transaction/verify/" + Uri.EscapeDataString(reference));
            using var res = await http.SendAsync(req, ct);
            var body = await res.Content.ReadFromJsonAsync<VerifyEnvelope>(cancellationToken: ct);
            if (!res.IsSuccessStatusCode || body is null || body.Data is null)
                return new PaystackVerify(false, null, 0, null, null, null, "unable_to_verify");
            return new PaystackVerify(true, body.Data.Status, body.Data.Amount, body.Data.Currency, body.Data.Reference, body.Data.Channel, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new PaystackVerify(false, null, 0, null, null, null, "provider_unavailable");
        }
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
        [JsonPropertyName("data")] public VerifyData? Data { get; set; }
    }
    private sealed class VerifyData
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("amount")] public long Amount { get; set; }
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("reference")] public string? Reference { get; set; }
        [JsonPropertyName("channel")] public string? Channel { get; set; }
    }
}
