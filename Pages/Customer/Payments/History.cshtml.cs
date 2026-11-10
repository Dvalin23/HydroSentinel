using HydroSentinel.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Pages.Customer.Payments;

public class HistoryModel(AppDbContext db) : CustomerPageModel
{
    public IReadOnlyList<Payment> Payments { get; private set; } = [];
    public IReadOnlyList<RecurringBillingTransaction> RecurringTransactions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var uid = UserId;
        if (uid is null) return SignInRequired();
        Payments = await db.Payments.AsNoTracking().Where(p => p.UserId == uid)
            .OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync(ct);
        RecurringTransactions = await db.RecurringBillingTransactions.AsNoTracking().Where(p => p.UserId == uid)
            .OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync(ct);
        return Page();
    }
}
