using System.Text.Json;
using HydroSentinel.Data;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HydroSentinel.Backend;

public sealed record RecurringCheckoutResult(bool Ok, string? Message, string? Reference, string? RedirectUrl);

/// <summary>Paystack monthly subscription enrollment, verification, refund, webhook settlement, and cancellation.</summary>
public sealed class RecurringBillingService(AppDbContext db, PaystackClient paystack, PlanCatalog catalog,
    UserManager<ApplicationUser> users, AuditService audit, IOptions<PaystackOptions> options, NotificationService notifications)
{
    public const long VerificationAmountMinor = 5000; // NGN 50.00

    public async Task<RecurringCheckoutResult> StartAsync(string userId, string? planId, string? idempotencyKey, bool acceptedTerms, string fallbackBaseUrl, CancellationToken ct)
    {
        var plan = catalog.Find(planId);
        if (plan is null) return new(false, "Choose a valid plan.", null, null);
        var code = options.Value.PlanCodeFor(plan.Id);
        if (string.IsNullOrWhiteSpace(code)) return new(false, "Monthly Paystack plan is not configured for this plan.", null, null);
        if (!paystack.Configured) return new(false, "Paystack is not configured for this environment.", null, null);
        if (!acceptedTerms) return new(false, "Please confirm the card verification charge and monthly auto-renewal terms.", null, null);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100) return new(false, "Checkout request is invalid. Refresh and try again.", null, null);

        var existing = await db.RecurringBillingAgreements.FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null)
            return new(true, null, existing.VerificationReference, null);
        if (await db.RecurringBillingAgreements.AnyAsync(x => x.UserId == userId &&
            (x.Status == RecurringBillingStatus.PendingCard || x.Status == RecurringBillingStatus.Finalizing || x.Status == RecurringBillingStatus.Trialing || x.Status == RecurringBillingStatus.Active || x.Status == RecurringBillingStatus.NonRenewing || x.Status == RecurringBillingStatus.PastDue
             || (x.Status == RecurringBillingStatus.Failed && db.RecurringBillingTransactions.Any(t => t.AgreementId == x.Id && t.Status != PaymentStatus.Failed))), ct))
            return new(false, "An automatic monthly subscription is already set up or awaiting confirmation. Manage it from Subscription.", null, null);

        var planCheck = await paystack.CheckMonthlyPlanAsync(code, BillingRules.ToMinor(plan.MonthlyPrice), ct);
        if (!planCheck.Ok) return new(false, "The configured Paystack plan does not match this monthly price. Contact support.", null, null);
        var me = await users.FindByIdAsync(userId);
        if (string.IsNullOrWhiteSpace(me?.Email)) return new(false, "Account email is unavailable.", null, null);

        var now = DateTimeOffset.UtcNow;
        var sub = await db.Subscriptions.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        DateTimeOffset startAt;
        bool trial = false;
        if (sub is null)
        {
            startAt = now.Add(BillingRules.TrialLength);
            trial = true;
            sub = new Subscription { UserId = userId, PlanId = plan.Id, PaidUntil = startAt, IsTrial = true };
            db.Subscriptions.Add(sub);
        }
        else if (sub.PaidUntil > now)
        {
            startAt = sub.PaidUntil;
            trial = sub.IsTrial;
        }
        else startAt = now;

        var reference = "HSV" + Guid.NewGuid().ToString("N")[..20].ToUpperInvariant();
        var agreement = new RecurringBillingAgreement
        {
            UserId = userId, PlanId = plan.Id, PaystackPlanCode = code, Status = RecurringBillingStatus.PendingCard,
            VerificationReference = reference, IdempotencyKey = idempotencyKey, TrialEndsAt = trial ? startAt : null,
            NextPaymentAt = startAt, CreatedAt = now, UpdatedAt = now
        };
        db.RecurringBillingAgreements.Add(agreement);
        db.RecurringBillingTransactions.Add(new RecurringBillingTransaction
        {
            Reference = reference, AgreementId = agreement.Id, UserId = userId, PlanId = plan.Id,
            Kind = "card_verification", AmountMinor = VerificationAmountMinor, CreatedAt = now
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return new(false, "Checkout is already being processed. Refresh Subscription in a moment.", null, null); }

        var baseUrl = string.IsNullOrWhiteSpace(options.Value.PublicBaseUrl) ? fallbackBaseUrl : options.Value.PublicBaseUrl.TrimEnd('/');
        var init = await paystack.InitializeAsync(me.Email, VerificationAmountMinor, "NGN", reference, baseUrl + "/pay/callback", ct, cardOnly: true);
        if (!init.Ok)
        {
            var verification = await db.RecurringBillingTransactions.FirstAsync(x => x.Reference == reference, ct);
            verification.Status = PaymentStatus.Failed;
            agreement.Status = RecurringBillingStatus.Failed;
            await db.SaveChangesAsync(ct);
            return new(false, "Card verification could not be started. No charge was made.", null, null);
        }
        await audit.WriteAsync(userId, "billing.recurring.start", reference, $"plan={plan.Id}; trial={trial}", ct);
        return new(true, null, reference, init.AuthorizationUrl);
    }

    /// <returns>null when the reference is an ordinary one-off payment; false when verification must be retried.</returns>
    public async Task<bool?> SettleReferenceAsync(string reference, CancellationToken ct)
    {
        var transaction = await db.RecurringBillingTransactions.FirstOrDefaultAsync(x => x.Reference == reference, ct);
        if (transaction is null) return null;
        var agreement = await db.RecurringBillingAgreements.FirstOrDefaultAsync(x => x.Id == transaction.AgreementId, ct);
        if (agreement is null) return true;
        var verify = await paystack.VerifyAsync(reference, ct);
        if (!verify.Reachable) return false;
        if (!string.Equals(verify.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            if (verify.Status is "failed" or "abandoned")
            {
                transaction.Status = PaymentStatus.Failed;
                agreement.Status = RecurringBillingStatus.Failed;
                agreement.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return true;
        }

        var expectedAmount = transaction.Kind == "card_verification"
            ? VerificationAmountMinor : catalog.Find(agreement.PlanId) is { } p ? BillingRules.ToMinor(p.MonthlyPrice) : -1;
        if (verify.Reference != reference || verify.AmountMinor != expectedAmount || !string.Equals(verify.Currency, "NGN", StringComparison.OrdinalIgnoreCase))
        {
            transaction.Status = PaymentStatus.AmountMismatch;
            agreement.Status = RecurringBillingStatus.Failed;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(agreement.UserId, "billing.recurring.amount_mismatch", reference, "Provider amount/currency did not match the expected charge.", ct);
            return true;
        }

        if (transaction.Kind == "card_verification")
        {
            if (agreement.Status != RecurringBillingStatus.PendingCard)
                return true;
            transaction.Status = PaymentStatus.Success;
            transaction.PaidAt ??= DateTimeOffset.UtcNow;
            transaction.Channel = verify.Channel;
            agreement.PaystackCustomerCode = verify.CustomerCode;
            agreement.CardBrand = verify.CardBrand;
            agreement.CardLast4 = verify.CardLast4;
            await db.SaveChangesAsync(ct);

            if (transaction.RefundStatus is null)
            {
                var refundClaim = await db.RecurringBillingTransactions.Where(x => x.Id == transaction.Id && x.RefundStatus == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RefundStatus, "requesting"), ct);
                if (refundClaim == 0) return true;
                transaction.RefundStatus = "requesting";
                var refund = await paystack.RefundAsync(reference, VerificationAmountMinor, ct);
                if (!refund.Ok)
                {
                    transaction.RefundStatus = "failed";
                    agreement.Status = RecurringBillingStatus.Failed;
                    await db.SaveChangesAsync(ct);
                    await audit.WriteAsync(agreement.UserId, "billing.recurring.refund_failed", reference, "Card verification succeeded; refund needs reconciliation.", ct);
                    return true;
                }
                transaction.RefundStatus = refund.Status ?? "pending";
                transaction.RefundReference = refund.RefundReference;
                await db.SaveChangesAsync(ct);
            }
            if (transaction.RefundStatus is "failed" or "needs-attention")
            {
                agreement.Status = RecurringBillingStatus.Failed;
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync(agreement.UserId, "billing.recurring.refund_attention", reference, $"Refund state={transaction.RefundStatus}", ct);
                return true;
            }

            if (!string.Equals(verify.Channel, "card", StringComparison.OrdinalIgnoreCase) || !verify.Reusable
                || string.IsNullOrWhiteSpace(verify.AuthorizationCode) || string.IsNullOrWhiteSpace(verify.CustomerCode))
            {
                agreement.Status = RecurringBillingStatus.Failed;
                agreement.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync(agreement.UserId, "billing.recurring.authorization_unusable", reference, "Verification was refunded; Paystack did not return a reusable card authorization.", ct);
                return true;
            }

            var setupClaim = await db.RecurringBillingAgreements.Where(x => x.Id == agreement.Id && x.Status == RecurringBillingStatus.PendingCard)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, RecurringBillingStatus.Finalizing), ct);
            if (setupClaim == 0) return true;

            var created = await paystack.CreateSubscriptionAsync(verify.CustomerCode!, verify.AuthorizationCode!, agreement.PaystackPlanCode, agreement.NextPaymentAt ?? DateTimeOffset.UtcNow, ct);
            if (!created.Ok)
            {
                agreement.Status = RecurringBillingStatus.Failed;
                agreement.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync(agreement.UserId, "billing.recurring.subscription_failed", reference, "Verification was refunded; subscription setup needs customer support.", ct);
                return true;
            }
            agreement.PaystackSubscriptionCode = created.SubscriptionCode;
            agreement.Status = agreement.NextPaymentAt > DateTimeOffset.UtcNow ? RecurringBillingStatus.Trialing : RecurringBillingStatus.Active;
            agreement.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(agreement.UserId, "billing.recurring.activated", reference, $"plan={agreement.PlanId}; next={agreement.NextPaymentAt:O}", ct);
            await notifications.AddAsync(agreement.UserId, "Monthly billing is set up", "Your card was verified. The first monthly charge is scheduled after your current trial or paid period.",
                "Billing", $"billing-start:{reference}", "/Customer/Subscription", ct);
            return true;
        }

        return await SettleRenewalAsync(transaction, agreement, verify, ct);
    }

    private async Task<bool> SettleRenewalAsync(RecurringBillingTransaction transaction, RecurringBillingAgreement agreement, PaystackVerify verify, CancellationToken ct)
    {
        var plan = catalog.Find(agreement.PlanId);
        var expected = plan is null ? -1 : BillingRules.ToMinor(plan.MonthlyPrice);
        if (verify.AmountMinor != expected || !string.Equals(verify.Currency, "NGN", StringComparison.OrdinalIgnoreCase))
        {
            await db.RecurringBillingTransactions.Where(x => x.Id == transaction.Id && x.Status == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, PaymentStatus.AmountMismatch), ct);
            await audit.WriteAsync(agreement.UserId, "billing.recurring.amount_mismatch", transaction.Reference, "Recurring charge did not match the expected monthly amount.", ct);
            return true;
        }
        var now = DateTimeOffset.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var claimed = await db.RecurringBillingTransactions.Where(x => x.Id == transaction.Id && x.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, PaymentStatus.Success).SetProperty(x => x.PaidAt, now).SetProperty(x => x.Channel, verify.Channel), ct);
        if (claimed == 0) { await tx.RollbackAsync(ct); return true; }
        var sub = await db.Subscriptions.FirstOrDefaultAsync(x => x.UserId == agreement.UserId, ct);
        if (sub is null) { sub = new Subscription { UserId = agreement.UserId, PlanId = agreement.PlanId, PaidUntil = now, IsTrial = false }; db.Subscriptions.Add(sub); }
        sub.PlanId = agreement.PlanId;
        sub.PaidUntil = (sub.PaidUntil > now ? sub.PaidUntil : now).AddMonths(1);
        sub.IsTrial = false;
        agreement.Status = RecurringBillingStatus.Active;
        agreement.NextPaymentAt = sub.PaidUntil;
        agreement.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await audit.WriteAsync(agreement.UserId, "billing.recurring.renewed", transaction.Reference, $"plan={agreement.PlanId}; until={sub.PaidUntil:O}", ct);
        await notifications.AddAsync(agreement.UserId, "Monthly payment confirmed", "Your subscription has been extended for another month.",
            "Billing", $"billing-renewal:{transaction.Reference}", "/Customer/Subscription", ct);
        return true;
    }

    public async Task HandleEventAsync(string eventName, JsonElement data, CancellationToken ct)
    {
        var code = ReadString(data, "subscription_code") ?? ReadNestedString(data, "subscription", "subscription_code");
        if (string.IsNullOrWhiteSpace(code)) return;
        var agreement = await db.RecurringBillingAgreements.FirstOrDefaultAsync(x => x.PaystackSubscriptionCode == code, ct);
        if (agreement is null) return;
        if (eventName == "subscription.not_renew") agreement.Status = RecurringBillingStatus.NonRenewing;
        else if (eventName == "subscription.disable") agreement.Status = RecurringBillingStatus.Cancelled;
        else if (eventName == "invoice.payment_failed") agreement.Status = RecurringBillingStatus.PastDue;
        else return;
        agreement.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(agreement.UserId, "billing.recurring." + eventName.Replace('.', '_'), code, "Provider subscription status updated.", ct);
        var title = eventName switch
        {
            "subscription.not_renew" => "Subscription will not renew",
            "subscription.disable" => "Subscription cancelled",
            _ => "Monthly payment failed"
        };
        var message = eventName switch
        {
            "subscription.not_renew" => "Your current access continues through the paid period. No next renewal is scheduled.",
            "subscription.disable" => "Your subscription was cancelled. Access remains through the current paid period.",
            _ => "We could not confirm your monthly payment. Your existing three-day grace period still applies."
        };
        var eventIdentity = ReadString(data, "invoice_code") ?? agreement.NextPaymentAt?.UtcTicks.ToString() ?? "status";
        await notifications.AddAsync(agreement.UserId, title, message, "Billing", $"billing-event:{eventName}:{code}:{eventIdentity}", "/Customer/Subscription", ct);
    }

    public async Task HandleRefundEventAsync(string eventName, JsonElement data, CancellationToken ct)
    {
        var reference = ReadString(data, "transaction_reference");
        if (string.IsNullOrWhiteSpace(reference)) return;
        var transaction = await db.RecurringBillingTransactions.FirstOrDefaultAsync(x => x.Reference == reference && x.Kind == "card_verification", ct);
        if (transaction is null) return;
        transaction.RefundStatus = eventName.StartsWith("refund.", StringComparison.Ordinal) ? eventName[7..] : eventName;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(transaction.UserId, "billing.recurring." + eventName.Replace('.', '_'), reference, "Verification refund status updated by Paystack.", ct);
        if (transaction.RefundStatus is "processed" or "failed" or "needs-attention")
        {
            var title = transaction.RefundStatus == "processed" ? "Verification refund processed" : "Verification refund needs attention";
            var message = transaction.RefundStatus == "processed"
                ? "Paystack reports the card verification refund has processed. Your bank may take additional time to show it."
                : "Paystack needs further action to complete the card verification refund. Contact support if it does not update.";
            await notifications.AddAsync(transaction.UserId, title, message, "Billing", $"billing-refund:{reference}:{transaction.RefundStatus}",
                "/Customer/Payments/History", ct);
        }
    }

    public async Task<bool> HandleRenewalEventAsync(string reference, JsonElement data, CancellationToken ct)
    {
        var subscriptionCode = ReadString(data, "subscription_code") ?? ReadNestedString(data, "subscription", "subscription_code");
        if (string.IsNullOrWhiteSpace(subscriptionCode)) return false;
        var agreement = await db.RecurringBillingAgreements.FirstOrDefaultAsync(x => x.PaystackSubscriptionCode == subscriptionCode, ct);
        if (agreement is null) return false;
        if (!await db.RecurringBillingTransactions.AnyAsync(x => x.Reference == reference, ct))
            db.RecurringBillingTransactions.Add(new RecurringBillingTransaction
            {
                Reference = reference, AgreementId = agreement.Id, UserId = agreement.UserId, PlanId = agreement.PlanId,
                Kind = "renewal", AmountMinor = catalog.Find(agreement.PlanId) is { } p ? BillingRules.ToMinor(p.MonthlyPrice) : 0
            });
        await db.SaveChangesAsync(ct);
        return await SettleReferenceAsync(reference, ct) ?? false;
    }

    public async Task<string?> ManagementLinkAsync(string userId, CancellationToken ct)
    {
        var agreement = await db.RecurringBillingAgreements.AsNoTracking().Where(x => x.UserId == userId && x.PaystackSubscriptionCode != null)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        return agreement?.PaystackSubscriptionCode is null ? null : await paystack.SubscriptionManagementLinkAsync(agreement.PaystackSubscriptionCode, ct);
    }

    public Task<RecurringBillingAgreement?> CurrentAsync(string userId, CancellationToken ct)
        => db.RecurringBillingAgreements.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);

    private static string? ReadString(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string? ReadNestedString(JsonElement e, string parent, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(parent, out var nested) ? ReadString(nested, name) : null;
}
