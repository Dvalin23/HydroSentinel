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
            // TODO(product): enable once an email sender exists: o.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(o =>
        {
            o.LoginPath = "/Login";
            o.AccessDeniedPath = "/Login";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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

        services.AddAntiforgery(o => { o.HeaderName = "RequestVerificationToken"; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; });

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
        services.AddScoped<CheckInService>();
        services.AddScoped<PaymentService>();
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
}
