using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HydroSentinel.Data;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HydroSentinel.Backend;

public static class Endpoints
{
    public const string AdminPolicy = "AdminOnly";
    public const string CustomerPolicy = "CustomerOnly";

    private static string UserId(ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private static IResult Problem(HttpContext ctx, int status, string code, string message)
        => Results.Problem(statusCode: status, title: message, extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["correlationId"] = ctx.TraceIdentifier
        });

    /// <summary>Antiforgery for JSON POSTs from the browser: token must be sent in the RequestVerificationToken header.</summary>
    private static RouteHandlerBuilder RequireAntiforgery(this RouteHandlerBuilder b) => b.AddEndpointFilter(async (ctx, next) =>
    {
        var af = ctx.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try { await af.ValidateRequestAsync(ctx.HttpContext); }
        catch (AntiforgeryValidationException) { return (object?)Problem(ctx.HttpContext, 400, "antiforgery_failed", "Request could not be verified."); }
        return (object?)await next(ctx);
    });

    public static void MapBackend(this WebApplication app)
    {
        MapDevice(app);
        MapPaystack(app);
        MapCustomer(app);
        MapAdmin(app);
    }

    // ---------------------------------------------------------------- device check-in
    // GET /api/device/checkin?id=&k=&w=&p=&m=&f=&lk=&b=&s=
    // Called by the HTTP relay (the SIM800L cannot do TLS). Response: "OK,<lock 0|1>,<unix time>" or "ERR".
    private static void MapDevice(WebApplication app)
    {
        app.MapGet("/api/device/checkin", async (HttpContext http, CheckInService svc, IOptions<DeviceOptions> opt, ILogger<CheckInService> log, CancellationToken ct) =>
        {
            var q = http.Request.Query;
            // Relay authenticates itself; unknown callers learn nothing.
            if (!DeviceKeys.SecretMatches(http.Request.Headers["X-Relay-Secret"].ToString(), opt.Value.RelaySecret))
                return Results.Text("ERR", "text/plain", Encoding.ASCII, 401);

            string id = q["id"].ToString(), k = q["k"].ToString(), w = q["w"].ToString();
            if (id.Length is 0 or > 64 || k.Length is 0 or > 128 || w.Length != 1)
                return Results.Text("ERR", "text/plain", Encoding.ASCII, 400);

            static bool Flag(string v) => v == "1";
            static int? Int(string v) => int.TryParse(v, out var n) ? n : null;

            var input = new CheckInInput(id, k, w, Flag(q["p"].ToString()), Flag(q["m"].ToString()),
                Int(q["f"].ToString()) ?? 0, Flag(q["lk"].ToString()), Int(q["b"].ToString()), Int(q["s"].ToString()));

            var result = await svc.HandleAsync(input, ct);
            if (!result.Authenticated)
            {
                log.LogWarning("Device check-in rejected for id {Serial}", id.Length > 32 ? id[..32] : id);
                return Results.Text("ERR", "text/plain", Encoding.ASCII, 401);
            }
            return Results.Text($"OK,{(result.Lock ? 1 : 0)},{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", "text/plain", Encoding.ASCII);
        }).AllowAnonymous().RequireRateLimiting("device");
    }

    // ---------------------------------------------------------------- Paystack
    private static void MapPaystack(WebApplication app)
    {
        // Webhook: signature over the RAW body. Always 200 once authenticated so Paystack stops retrying;
        // settlement re-verifies with Paystack and is idempotent.
        app.MapPost("/api/paystack/webhook", async (HttpContext http, PaystackClient paystack, PaymentService payments, CancellationToken ct) =>
        {
            using var ms = new MemoryStream();
            await http.Request.Body.CopyToAsync(ms, ct);
            var raw = ms.ToArray();
            if (raw.Length is 0 or > 64 * 1024) return Results.StatusCode(400);
            if (!paystack.SignatureValid(raw, http.Request.Headers["x-paystack-signature"].ToString()))
                return Results.StatusCode(401);

            string? ev = null, reference = null;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                ev = doc.RootElement.GetProperty("event").GetString();
                reference = doc.RootElement.GetProperty("data").GetProperty("reference").GetString();
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return Results.StatusCode(400);
            }

            if (ev == "charge.success" && !string.IsNullOrWhiteSpace(reference))
            {
                var outcome = await payments.VerifyAndSettleAsync(reference!, ct);
                if (outcome == SettleOutcome.Unverifiable) return Results.StatusCode(503); // ask Paystack to retry
            }
            return Results.Ok();
        }).AllowAnonymous().DisableAntiforgery();

        // Browser comes back here after paying. We verify server-side, then show the result page.
        app.MapGet("/pay/callback", async (string? reference, PaymentService payments, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(reference) || reference.Length > 64) return Results.Redirect("/Customer/Payments/Result");
            await payments.VerifyAndSettleAsync(reference, ct);
            return Results.Redirect("/Customer/Payments/Result?reference=" + Uri.EscapeDataString(reference));
        }).AllowAnonymous();
    }

    // ---------------------------------------------------------------- customer API
    private static void MapCustomer(WebApplication app)
    {
        var g = app.MapGroup("/api/customer").RequireAuthorization(CustomerPolicy);

        g.MapGet("/antiforgery", (HttpContext http, IAntiforgery af) =>
        {
            var t = af.GetAndStoreTokens(http);
            return Results.Ok(new { headerName = t.HeaderName, token = t.RequestToken });
        });

        g.MapGet("/devices", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var uid = UserId(user);
            var now = DateTimeOffset.UtcNow;
            var sub = await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == uid, ct);
            var devices = await db.Devices.AsNoTracking().Where(d => d.OwnerId == uid).OrderBy(d => d.Name).ToListAsync(ct);
            return Results.Ok(new
            {
                items = devices.Select(d => ToDto(d, sub, now)),
                sourceStatus = "live"
            });
        });

        g.MapGet("/devices/{id:guid}", async (Guid id, ClaimsPrincipal user, AppDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var uid = UserId(user);
            var d = await db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct);
            if (d is null) return Problem(http, 404, "device_not_found", "Device not found.");
            var sub = await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == uid, ct);
            return Results.Ok(ToDto(d, sub, DateTimeOffset.UtcNow));
        });

        g.MapGet("/devices/{id:guid}/readings", async (Guid id, DateTimeOffset? from, DateTimeOffset? to, int? limit,
            ClaimsPrincipal user, AppDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var uid = UserId(user);
            var d = await db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct);
            if (d is null) return Problem(http, 404, "device_not_found", "Device not found.");

            var now = DateTimeOffset.UtcNow;
            var f = (from ?? now.AddDays(-1)).ToUniversalTime();
            var t = (to ?? now).ToUniversalTime();
            if (f >= t) return Problem(http, 422, "invalid_range", "'from' must be before 'to'.");
            if (t - f > TimeSpan.FromDays(31)) return Problem(http, 422, "range_too_large", "Maximum range is 31 days.");
            var n = Math.Clamp(limit ?? 200, 1, 200);

            var points = await db.Readings.AsNoTracking()
                .Where(r => r.DeviceId == id && r.At >= f && r.At <= t)
                .OrderByDescending(r => r.At).Take(n).ToListAsync(ct);
            return Results.Ok(new
            {
                deviceId = id,
                range = new { from = f, to = t },
                freshness = new { state = BillingRules.Freshness(d, now), lastUpdatedAt = d.LastSeenAt },
                points = points.Select(r => new
                {
                    timestamp = r.At, tankLevel = r.TankLevel, pumpOn = r.PumpOn, mainsOn = r.MainsOn,
                    faultCode = r.FaultCode, batteryPercent = r.BatteryPercent, signalCsq = r.SignalCsq
                })
            });
        });

        g.MapGet("/alerts", async (string? status, string? severity, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var uid = UserId(user);
            var q = from e in db.DeviceEvents.AsNoTracking()
                    join d in db.Devices.AsNoTracking() on e.DeviceId equals d.Id
                    where d.OwnerId == uid && e.Severity != "info"
                    select new { e, d.Name };
            if (status == "open") q = q.Where(x => x.e.AcknowledgedAt == null);
            if (status == "acknowledged") q = q.Where(x => x.e.AcknowledgedAt != null);
            if (severity is "critical" or "warning") q = q.Where(x => x.e.Severity == severity);
            var items = await q.OrderByDescending(x => x.e.At).Take(100).ToListAsync(ct);
            return Results.Ok(new
            {
                items = items.Select(x => new
                {
                    id = x.e.Id, severity = x.e.Severity, type = x.e.Type, deviceId = x.e.DeviceId, deviceName = x.Name,
                    description = x.e.Detail, occurredAt = x.e.At,
                    status = x.e.AcknowledgedAt == null ? "open" : "acknowledged"
                }),
                sourceStatus = "live"
            });
        });

        g.MapPost("/alerts/{id:long}/acknowledge", async (long id, ClaimsPrincipal user, AppDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var uid = UserId(user);
            var ev = await (from e in db.DeviceEvents join d in db.Devices on e.DeviceId equals d.Id
                            where e.Id == id && d.OwnerId == uid select e).FirstOrDefaultAsync(ct);
            if (ev is null) return Problem(http, 404, "alert_not_found", "Alert not found.");
            if (ev.AcknowledgedAt is null) // idempotent
            {
                ev.AcknowledgedAt = DateTimeOffset.UtcNow;
                ev.AcknowledgedBy = uid;
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new { id = ev.Id, status = "acknowledged", acknowledgedAt = ev.AcknowledgedAt });
        }).RequireAntiforgery();

        g.MapGet("/plans", (PlanCatalog catalog) => Results.Ok(new
        {
            currency = PlanCatalog.Currency,
            note = "Hardware is sold separately. Checkout charges the monthly service fee only.",
            plans = catalog.Plans.Select(p => new { id = p.Id, name = p.Name, description = p.Description, deviceAllowance = p.DeviceAllowance, hardwarePrice = p.HardwarePrice, monthlyPrice = p.MonthlyPrice })
        }));

        g.MapGet("/subscription", async (ClaimsPrincipal user, AppDbContext db, PlanCatalog catalog, CancellationToken ct) =>
        {
            var uid = UserId(user);
            var sub = await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == uid, ct);
            var now = DateTimeOffset.UtcNow;
            var state = BillingRules.StateOf(sub, now);
            return Results.Ok(new
            {
                status = state.ToString().ToLowerInvariant(),
                planId = sub?.PlanId,
                planName = sub is null ? null : catalog.Find(sub.PlanId)?.Name,
                isTrial = sub?.IsTrial,
                paidUntil = sub?.PaidUntil,
                graceEndsAt = sub is null ? (DateTimeOffset?)null : sub.PaidUntil + BillingRules.GracePeriod,
                sourceStatus = "live"
            });
        });

        // Server re-prices from PlanCatalog. Client sends only a plan id, month count and idempotency key.
        g.MapPost("/payments/checkout", async (CheckoutRequest req, ClaimsPrincipal user, PaymentService payments, HttpContext http, CancellationToken ct) =>
        {
            var r = await payments.StartCheckoutAsync(UserId(user), req.PlanId, req.Months, req.IdempotencyKey, $"{http.Request.Scheme}://{http.Request.Host}", ct);
            if (!r.Ok) return Problem(http, r.Status, r.Code!, r.Message!);
            return Results.Ok(new { reference = r.Reference, status = r.PaymentStatus, redirectUrl = r.RedirectUrl, replay = r.Replay });
        }).RequireAntiforgery();

        g.MapGet("/payments", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var uid = UserId(user);
            var items = await db.Payments.AsNoTracking().Where(p => p.UserId == uid).OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync(ct);
            return Results.Ok(new
            {
                items = items.Select(p => new { id = p.Id, reference = p.Reference, createdAt = p.CreatedAt, paidAt = p.PaidAt, planId = p.PlanId,
                    months = p.Months, amount = p.AmountMinor / 100m, currency = p.Currency, status = p.Status, method = p.Channel }),
                sourceStatus = "live"
            });
        });

        // Lookup by reference; a still-pending payment is re-verified with Paystack.
        g.MapGet("/payments/{reference}", async (string reference, ClaimsPrincipal user, AppDbContext db, PaymentService payments, HttpContext http, CancellationToken ct) =>
        {
            var uid = UserId(user);
            var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Reference == reference && x.UserId == uid, ct);
            if (p is null) return Problem(http, 404, "payment_not_found", "Payment not found.");
            var sourceStatus = "live";
            if (p.Status == PaymentStatus.Pending)
            {
                var outcome = await payments.VerifyAndSettleAsync(reference, ct);
                if (outcome == SettleOutcome.Unverifiable) sourceStatus = "unableToVerify";
                p = await db.Payments.AsNoTracking().FirstAsync(x => x.Reference == reference, ct);
            }
            return Results.Ok(new { reference = p.Reference, status = p.Status, amount = p.AmountMinor / 100m, currency = p.Currency,
                planId = p.PlanId, paidAt = p.PaidAt, sourceStatus });
        });
    }

    private static object ToDto(Device d, Subscription? sub, DateTimeOffset now)
    {
        var state = BillingRules.StateOf(sub, now);
        return new
        {
            id = d.Id, name = d.Name,
            serialMasked = d.Serial.Length <= 4 ? "****" : new string('*', d.Serial.Length - 4) + d.Serial[^4..],
            status = BillingRules.Freshness(d, now),
            lastSeenAt = d.LastSeenAt,
            tankLevel = d.TankLevel, pumpOn = d.PumpOn, mainsOn = d.MainsOn, faultCode = d.FaultCode,
            batteryPercent = d.BatteryPercent, signalCsq = d.SignalCsq,
            pumpLocked = d.ReportedLock, // what the unit itself last reported
            subscriptionStatus = state.ToString().ToLowerInvariant()
        };
    }

    // ---------------------------------------------------------------- admin API
    private static void MapAdmin(WebApplication app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization(AdminPolicy);

        g.MapGet("/antiforgery", (HttpContext http, IAntiforgery af) =>
        {
            var t = af.GetAndStoreTokens(http);
            return Results.Ok(new { headerName = t.HeaderName, token = t.RequestToken });
        });

        g.MapGet("/devices", async (AppDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var rows = await (from d in db.Devices.AsNoTracking()
                              join u in db.Users.AsNoTracking() on d.OwnerId equals u.Id into uj
                              from u in uj.DefaultIfEmpty()
                              orderby d.Serial
                              select new { d, OwnerEmail = u == null ? null : u.Email }).ToListAsync(ct);
            var subs = await db.Subscriptions.AsNoTracking().ToDictionaryAsync(s => s.UserId, ct);
            return Results.Ok(new
            {
                items = rows.Select(r =>
                {
                    subs.TryGetValue(r.d.OwnerId ?? "", out var sub);
                    return new
                    {
                        id = r.d.Id, serial = r.d.Serial, name = r.d.Name, ownerEmail = r.OwnerEmail,
                        status = BillingRules.Freshness(r.d, now), lastSeenAt = r.d.LastSeenAt,
                        tankLevel = r.d.TankLevel, pumpOn = r.d.PumpOn, mainsOn = r.d.MainsOn,
                        adminLock = r.d.AdminLock, adminLockReason = r.d.AdminLockReason,
                        subscriptionStatus = BillingRules.StateOf(sub, now).ToString().ToLowerInvariant(),
                        deviceReportsLocked = r.d.ReportedLock
                    };
                })
            });
        });

        // Register a unit. Returns the raw key ONCE; only its hash is stored.
        g.MapPost("/devices", async (RegisterDeviceRequest req, AppDbContext db, ClaimsPrincipal user, AuditService audit, HttpContext http, CancellationToken ct) =>
        {
            var serial = (req.Serial ?? "").Trim();
            if (serial.Length is < 3 or > 64) return Problem(http, 422, "invalid_serial", "Serial must be 3-64 characters.");
            if (await db.Devices.AnyAsync(d => d.Serial == serial, ct)) return Problem(http, 409, "duplicate_serial", "A device with that serial already exists.");
            var key = DeviceKeys.NewKey();
            var d = new Device { Serial = serial, Name = string.IsNullOrWhiteSpace(req.Name) ? serial : req.Name.Trim(), KeyHash = DeviceKeys.Hash(key) };
            db.Devices.Add(d);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(UserId(user), "device.register", d.Id.ToString(), serial, ct);
            return Results.Ok(new { id = d.Id, serial = d.Serial, deviceKey = key, note = "Store this key in the controller now. It cannot be shown again." });
        }).RequireAntiforgery();

        g.MapPost("/devices/{id:guid}/rotate-key", async (Guid id, AppDbContext db, ClaimsPrincipal user, AuditService audit, HttpContext http, CancellationToken ct) =>
        {
            var d = await db.Devices.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null) return Problem(http, 404, "device_not_found", "Device not found.");
            var key = DeviceKeys.NewKey();
            d.KeyHash = DeviceKeys.Hash(key);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(UserId(user), "device.rotate_key", d.Id.ToString(), null, ct);
            return Results.Ok(new { id = d.Id, deviceKey = key });
        }).RequireAntiforgery();

        // Assign to a customer by email. First assignment starts a 7-day trial if they have no subscription.
        g.MapPost("/devices/{id:guid}/assign", async (Guid id, AssignRequest req, AppDbContext db, UserManager<ApplicationUser> users,
            ClaimsPrincipal user, AuditService audit, HttpContext http, CancellationToken ct) =>
        {
            var d = await db.Devices.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null) return Problem(http, 404, "device_not_found", "Device not found.");
            var owner = string.IsNullOrWhiteSpace(req.Email) ? null : await users.FindByEmailAsync(req.Email.Trim());
            if (owner is null) return Problem(http, 404, "customer_not_found", "No customer with that email.");
            if (!await users.IsInRoleAsync(owner, Roles.Customer)) return Problem(http, 422, "not_a_customer", "That account is not a customer.");

            d.OwnerId = owner.Id;
            if (!await db.Subscriptions.AnyAsync(s => s.UserId == owner.Id, ct))
                db.Subscriptions.Add(new Subscription { UserId = owner.Id, PlanId = "basic", IsTrial = true, PaidUntil = DateTimeOffset.UtcNow + BillingRules.TrialLength });
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(UserId(user), "device.assign", d.Id.ToString(), owner.Email, ct);
            return Results.Ok(new { id = d.Id, ownerEmail = owner.Email });
        }).RequireAntiforgery();

        // Remote lock / unlock. Gated by Features:RemoteLockEnabled (OFF by default - see FeatureOptions).
        g.MapPost("/devices/{id:guid}/lock", async (Guid id, LockRequest req, AppDbContext db, IOptions<FeatureOptions> features,
            ClaimsPrincipal user, AuditService audit, HttpContext http, CancellationToken ct) =>
        {
            if (!features.Value.RemoteLockEnabled)
                return Problem(http, 403, "remote_lock_disabled", "Remote lock is not enabled. It requires explicit approval; see Features:RemoteLockEnabled.");
            var d = await db.Devices.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null) return Problem(http, 404, "device_not_found", "Device not found.");
            if (req.Locked && string.IsNullOrWhiteSpace(req.Reason)) return Problem(http, 422, "reason_required", "A reason is required to lock a device.");
            d.AdminLock = req.Locked;
            d.AdminLockReason = req.Locked ? req.Reason!.Trim() : null;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(UserId(user), req.Locked ? "device.lock" : "device.unlock", d.Id.ToString(), req.Reason, ct);
            // The unit picks this up at its next check-in (it polls; it cannot be pushed to).
            return Results.Ok(new { id = d.Id, adminLock = d.AdminLock, appliesAtNextCheckIn = true });
        }).RequireAntiforgery();

        g.MapGet("/payments", async (AppDbContext db, CancellationToken ct) =>
        {
            var rows = await (from p in db.Payments.AsNoTracking()
                              join u in db.Users.AsNoTracking() on p.UserId equals u.Id
                              orderby p.CreatedAt descending
                              select new { p, u.Email }).Take(200).ToListAsync(ct);
            return Results.Ok(new
            {
                items = rows.Select(r => new { reference = r.p.Reference, customer = r.Email, planId = r.p.PlanId, months = r.p.Months,
                    amount = r.p.AmountMinor / 100m, currency = r.p.Currency, status = r.p.Status, createdAt = r.p.CreatedAt, paidAt = r.p.PaidAt })
            });
        });

        g.MapGet("/subscriptions", async (AppDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var rows = await (from s in db.Subscriptions.AsNoTracking()
                              join u in db.Users.AsNoTracking() on s.UserId equals u.Id
                              orderby s.PaidUntil select new { s, u.Email }).ToListAsync(ct);
            return Results.Ok(new
            {
                items = rows.Select(r => new { customer = r.Email, planId = r.s.PlanId, isTrial = r.s.IsTrial, paidUntil = r.s.PaidUntil,
                    status = BillingRules.StateOf(r.s, now).ToString().ToLowerInvariant() })
            });
        });

        g.MapGet("/audit", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AuditEntries.AsNoTracking().OrderByDescending(a => a.At).Take(200).ToListAsync(ct)));
    }
}

public sealed record CheckoutRequest(string? PlanId, int Months, string? IdempotencyKey);
public sealed record RegisterDeviceRequest(string? Serial, string? Name);
public sealed record AssignRequest(string? Email);
public sealed record LockRequest(bool Locked, string? Reason);
