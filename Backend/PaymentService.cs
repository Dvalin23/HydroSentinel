using HydroSentinel.Data;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HydroSentinel.Backend;

public enum SettleOutcome { Activated, AlreadyProcessed, StillPending, Failed, AmountMismatch, NotFound, Unverifiable }

public sealed record CheckoutResult(bool Ok, int Status, string? Code, string? Message, string? Reference, string? PaymentStatus, string? RedirectUrl, bool Replay);

public sealed class PaymentService(AppDbContext db, PaystackClient paystack, PlanCatalog catalog, AuditService audit,
    UserManager<ApplicationUser> users, IOptions<PaystackOptions> popt)
{
    /// <summary>
    /// Verify with Paystack, then settle exactly once. Used by both the browser callback/lookup and the webhook,
    /// so a redirect alone never activates anything and duplicate deliveries are harmless.
    /// </summary>
    public async Task<SettleOutcome> VerifyAndSettleAsync(string reference, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Reference == reference, ct);
        if (payment is null) return SettleOutcome.NotFound;
        if (payment.Status == PaymentStatus.Success) return SettleOutcome.AlreadyProcessed;

        var v = await paystack.VerifyAsync(reference, ct);
        if (!v.Reachable) return SettleOutcome.Unverifiable;

        if (!string.Equals(v.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(v.Status, "failed", StringComparison.OrdinalIgnoreCase) || string.Equals(v.Status, "abandoned", StringComparison.OrdinalIgnoreCase))
            {
                await db.Payments.Where(p => p.Reference == reference && p.Status == PaymentStatus.Pending)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Failed), ct);
                await audit.WriteAsync(null, "payment.failed", reference, v.Status, ct);
                return SettleOutcome.Failed;
            }
            return SettleOutcome.StillPending;
        }

        // Reference, amount and currency must all match what WE recorded when we priced it.
        if (!string.Equals(v.Reference, payment.Reference, StringComparison.Ordinal)
            || v.AmountMinor != payment.AmountMinor
            || !string.Equals(v.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            await db.Payments.Where(p => p.Reference == reference && p.Status == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.AmountMismatch), ct);
            await audit.WriteAsync(null, "payment.amount_mismatch", reference, $"paid={v.AmountMinor} {v.Currency} expected={payment.AmountMinor} {payment.Currency}", ct);
            return SettleOutcome.AmountMismatch;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Atomic claim: only one caller flips pending -> success.
        var now = DateTimeOffset.UtcNow;
        var claimed = await db.Payments
            .Where(p => p.Reference == reference && p.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PaymentStatus.Success)
                .SetProperty(p => p.PaidAt, now)
                .SetProperty(p => p.Channel, v.Channel), ct);
        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            return SettleOutcome.AlreadyProcessed;
        }

        var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == payment.UserId, ct);
        if (sub is null)
        {
            sub = new Subscription { UserId = payment.UserId, PlanId = payment.PlanId, PaidUntil = BillingRules.Extend(null, payment.Months, now), IsTrial = false };
            db.Subscriptions.Add(sub);
        }
        else
        {
            sub.PlanId = payment.PlanId;
            sub.PaidUntil = BillingRules.Extend(sub.PaidUntil, payment.Months, now);
            sub.IsTrial = false;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await audit.WriteAsync(payment.UserId, "payment.success", reference, $"plan={payment.PlanId} months={payment.Months}", ct);
        return SettleOutcome.Activated;
    }

    public PlanDefinition? Plan(string? id) => catalog.Find(id);

    private static CheckoutResult Fail(int status, string code, string message) => new(false, status, code, message, null, null, null, false);

    /// <summary>
    /// Start a checkout. The price is computed HERE from the PlanCatalog; the caller supplies only plan id,
    /// month count and an idempotency key. Only the monthly service fee is charged (hardware is sold separately).
    /// </summary>
    public async Task<CheckoutResult> StartCheckoutAsync(string userId, string? planId, int months, string? idempotencyKey, string fallbackBaseUrl, CancellationToken ct)
    {
        var plan = catalog.Find(planId);
        if (plan is null) return Fail(422, "invalid_plan", "Unknown plan.");
        if (months is < 1 or > 12) return Fail(422, "invalid_months", "Months must be between 1 and 12.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100) return Fail(422, "invalid_idempotency_key", "An idempotency key is required.");

        var existing = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.PlanId != plan.Id || existing.Months != months) return Fail(409, "idempotency_conflict", "That key was already used for a different request.");
            return new CheckoutResult(true, 200, null, null, existing.Reference, existing.Status, null, true);
        }

        var me = await users.FindByIdAsync(userId);
        if (me?.Email is null) return Fail(403, "forbidden", "Account is not eligible.");

        var minor = BillingRules.ToMinor(plan.MonthlyPrice * months);
        var payment = new Payment
        {
            Reference = "HS" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant(),
            UserId = userId, PlanId = plan.Id, Months = months, AmountMinor = minor,
            Currency = PlanCatalog.Currency, IdempotencyKey = idempotencyKey
        };
        db.Payments.Add(payment);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Fail(409, "idempotency_conflict", "Duplicate request."); }

        var baseUrl = string.IsNullOrWhiteSpace(popt.Value.PublicBaseUrl) ? fallbackBaseUrl : popt.Value.PublicBaseUrl.TrimEnd('/');
        var init = await paystack.InitializeAsync(me.Email, minor, payment.Currency, payment.Reference, baseUrl + "/pay/callback", ct);
        if (!init.Ok)
        {
            payment.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync(ct);
            return Fail(init.Error == "payments_not_configured" ? 503 : 502, init.Error ?? "provider_error", "Payment could not be started. You have not been charged.");
        }
        await audit.WriteAsync(userId, "payment.start", payment.Reference, $"plan={plan.Id} months={months} amount={minor}", ct);
        return new CheckoutResult(true, 200, null, null, payment.Reference, payment.Status, init.AuthorizationUrl, false);
    }
}
