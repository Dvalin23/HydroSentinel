using HydroSentinel.Data;

namespace HydroSentinel.Backend;

public sealed class AuditService(AppDbContext db, IHttpContextAccessor accessor)
{
    public async Task WriteAsync(string? actorId, string action, string? target, string? detail, CancellationToken ct = default)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            ActorId = actorId,
            Action = action,
            Target = target,
            Detail = detail is { Length: > 500 } ? detail[..500] : detail,
            CorrelationId = accessor.HttpContext?.TraceIdentifier
        });
        await db.SaveChangesAsync(ct);
    }
}
