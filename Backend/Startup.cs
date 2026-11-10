using System.Threading.RateLimiting;
using HydroSentinel.Data;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Backend;

public static class Startup
{
    public static void AddBackend(this WebApplicationBuilder builder)
    {
        var cfg = builder.Configuration;
        var services = builder.Services;

        services.Configure<PaystackOptions>(cfg.GetSection(PaystackOptions.Section));
        services.Configure<DeviceOptions>(cfg.GetSection(DeviceOptions.Section));
        services.Configure<FeatureOptions>(cfg.GetSection(FeatureOptions.Section));
        services.Configure<SeedOptions>(cfg.GetSection(SeedOptions.Section));
        services.Configure<EmailOptions>(cfg.GetSection(EmailOptions.Section));

        // Provider: "Sqlite" (default, dev) or "Postgres" (prod). Connection string: ConnectionStrings:Default
        var provider = cfg["Database:Provider"] ?? "Sqlite";
        var conn = cfg.GetConnectionString("Default") ?? "Data Source=hydrosentinel.db";
        services.AddDbContext<AppDbContext>(o =>
        {
            if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)) o.UseNpgsql(conn);
            else o.UseSqlite(conn);
        });

        services.AddIdentity<ApplicationUser, IdentityRole>(o =>
        {
            o.Password.RequiredLength = 10;
            o.Password.RequireNonAlphanumeric = false;
            o.User.RequireUniqueEmail = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            o.Lockout.AllowedForNewUsers = true;
            o.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(o =>
        {
            o.LoginPath = "/Login";
            o.AccessDeniedPath = "/Login";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            // Local developers may run the HTTP launch profile without a dev certificate.
            // Keep cookies secure in production; mirror the request scheme only in Development.
            o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = true;
            // API callers get status codes, not redirects to an HTML login page.
            o.Events.OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 401; else ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = 403; else ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Endpoints.AdminPolicy, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin))
            .AddPolicy(Endpoints.CustomerPolicy, p => p.RequireAuthenticatedUser().RequireRole(Roles.Customer));

        services.AddAntiforgery(o =>
        {
            o.HeaderName = "RequestVerificationToken";
            o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            // Per device serial so one noisy unit cannot starve others (the relay shares one IP).
            o.AddPolicy("device", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Request.Query["id"].ToString(),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        services.AddHttpContextAccessor();
        services.AddScoped<AuditService>();
        services.AddScoped<AccountEmailSender>();
        services.AddScoped<IEmailSender<ApplicationUser>>(sp => sp.GetRequiredService<AccountEmailSender>());
        services.AddScoped<AccountEmailLinks>();
        services.AddScoped<SupportTicketService>();
        services.AddScoped<CheckInService>();
        services.AddScoped<DeviceClaimService>();
        services.AddScoped<CustomerPortal>();
        services.AddScoped<PaymentService>();
        services.AddScoped<RecurringBillingService>();
        services.AddScoped<NotificationService>();
        services.AddHttpClient<PaystackClient>(c => c.Timeout = TimeSpan.FromSeconds(15));
    }

    /// <summary>Create the schema (dev) and seed roles + the first admin from configuration.</summary>
    public static async Task InitialiseBackendAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        // Production: run `dotnet ef database update` from a migration instead.
        if (app.Environment.IsDevelopment()) await db.Database.EnsureCreatedAsync();

        // The project predates an EF migration baseline. Create the additive support tables
        // idempotently so existing development databases can use the new feature immediately.
        await EnsureSupportTablesAsync(db, app.Configuration["Database:Provider"] ?? "Sqlite");

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in new[] { Roles.Admin, Roles.Customer })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        var seed = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeedOptions>>().Value;
        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword)) return;

        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(seed.AdminEmail) is null)
        {
            var admin = new ApplicationUser { UserName = seed.AdminEmail, Email = seed.AdminEmail, EmailConfirmed = true, FullName = "Administrator" };
            var created = await users.CreateAsync(admin, seed.AdminPassword);
            if (created.Succeeded) await users.AddToRoleAsync(admin, Roles.Admin);
            else app.Logger.LogError("Admin seed failed: {Errors}", string.Join("; ", created.Errors.Select(e => e.Code)));
        }
    }

    private static async Task EnsureSupportTablesAsync(AppDbContext db, string provider)
    {
        var idType = provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ? "uuid" : "TEXT";
        var sql = $"""
            CREATE TABLE IF NOT EXISTS "SupportTickets" (
                "Id" {idType} NOT NULL PRIMARY KEY,
                "Reference" TEXT NOT NULL,
                "UserId" TEXT NOT NULL,
                "Subject" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "DeviceId" {idType} NULL,
                "Status" TEXT NOT NULL,
                "CreatedAt" BIGINT NOT NULL,
                "UpdatedAt" BIGINT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SupportTickets_Reference" ON "SupportTickets" ("Reference");
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_UserId_UpdatedAt" ON "SupportTickets" ("UserId", "UpdatedAt");
            CREATE TABLE IF NOT EXISTS "SupportMessages" (
                "Id" {idType} NOT NULL PRIMARY KEY,
                "TicketId" {idType} NOT NULL REFERENCES "SupportTickets" ("Id") ON DELETE CASCADE,
                "AuthorId" TEXT NULL,
                "IsStaffReply" BOOLEAN NOT NULL,
                "Body" TEXT NOT NULL,
                "CreatedAt" BIGINT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_SupportMessages_TicketId_CreatedAt" ON "SupportMessages" ("TicketId", "CreatedAt");
            CREATE TABLE IF NOT EXISTS "RecurringBillingAgreements" (
                "Id" {idType} NOT NULL PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "PlanId" TEXT NOT NULL,
                "PaystackPlanCode" TEXT NOT NULL,
                "PaystackCustomerCode" TEXT NULL,
                "PaystackSubscriptionCode" TEXT NULL,
                "Status" TEXT NOT NULL,
                "VerificationReference" TEXT NULL,
                "IdempotencyKey" TEXT NOT NULL,
                "CardBrand" TEXT NULL,
                "CardLast4" TEXT NULL,
                "TrialEndsAt" BIGINT NULL,
                "NextPaymentAt" BIGINT NULL,
                "CreatedAt" BIGINT NOT NULL,
                "UpdatedAt" BIGINT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecurringBillingAgreements_PaystackSubscriptionCode" ON "RecurringBillingAgreements" ("PaystackSubscriptionCode");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecurringBillingAgreements_VerificationReference" ON "RecurringBillingAgreements" ("VerificationReference");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecurringBillingAgreements_UserId_IdempotencyKey" ON "RecurringBillingAgreements" ("UserId", "IdempotencyKey");
            CREATE INDEX IF NOT EXISTS "IX_RecurringBillingAgreements_UserId" ON "RecurringBillingAgreements" ("UserId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecurringBillingAgreements_UserId_Current" ON "RecurringBillingAgreements" ("UserId")
                WHERE "Status" IN ('pending_card','finalizing','trialing','active','non_renewing','past_due');
            CREATE TABLE IF NOT EXISTS "RecurringBillingTransactions" (
                "Id" {idType} NOT NULL PRIMARY KEY,
                "Reference" TEXT NOT NULL,
                "AgreementId" {idType} NOT NULL REFERENCES "RecurringBillingAgreements" ("Id") ON DELETE CASCADE,
                "UserId" TEXT NOT NULL,
                "PlanId" TEXT NOT NULL,
                "Kind" TEXT NOT NULL,
                "AmountMinor" BIGINT NOT NULL,
                "Currency" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "Channel" TEXT NULL,
                "RefundStatus" TEXT NULL,
                "RefundReference" TEXT NULL,
                "CreatedAt" BIGINT NOT NULL,
                "PaidAt" BIGINT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecurringBillingTransactions_Reference" ON "RecurringBillingTransactions" ("Reference");
            CREATE INDEX IF NOT EXISTS "IX_RecurringBillingTransactions_AgreementId_CreatedAt" ON "RecurringBillingTransactions" ("AgreementId", "CreatedAt");
            CREATE TABLE IF NOT EXISTS "CustomerNotifications" (
                "Id" {idType} NOT NULL PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Message" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "CreatedAt" BIGINT NOT NULL,
                "IsRead" BOOLEAN NOT NULL,
                "ReadAt" BIGINT NULL,
                "Destination" TEXT NULL,
                "DedupeKey" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CustomerNotifications_DedupeKey" ON "CustomerNotifications" ("DedupeKey");
            CREATE INDEX IF NOT EXISTS "IX_CustomerNotifications_UserId_CreatedAt" ON "CustomerNotifications" ("UserId", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_CustomerNotifications_UserId_IsRead_CreatedAt" ON "CustomerNotifications" ("UserId", "IsRead", "CreatedAt");
            """;
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
