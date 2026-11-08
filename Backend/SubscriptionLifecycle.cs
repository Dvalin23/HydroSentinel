using HydroSentinel.Data;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Backend;

/// <summary>Subscription side-effects that both admin assignment and customer self-claim share.</summary>
public static class SubscriptionLifecycle
{
    /// <summary>First device on an account starts the 7-day trial. Never downgrades an existing plan.</summary>
    public static async Task EnsureTrialAsync(this AppDbContext db, string userId, CancellationToken ct)
    {
        if (await db.Subscriptions.AnyAsync(s => s.UserId == userId, ct)) return;
        db.Subscriptions.Add(new Subscription
        {
            UserId = userId,
            PlanId = "basic",
            IsTrial = true,
            PaidUntil = DateTimeOffset.UtcNow + BillingRules.TrialLength
        });
    }
}
