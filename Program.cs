using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using HydroSentinel.Backend;
using HydroSentinel.Services;

var builder = WebApplication.CreateBuilder(args);

// The Windows EventLog provider requires permission to write to the machine's
// event sources. Keep Development diagnostics on the console so a restricted
// developer account can still receive startup/request errors. Production keeps
// the host's configured logging providers and must use an approved log sink.
if (builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();

    // Use a project-local development key ring when the user profile's
    // default key directory is unavailable. This keeps antiforgery and other
    // protected development payloads working without changing production key
    // persistence or deleting an existing key ring.
    var developmentKeyPath = Path.Combine(builder.Environment.ContentRootPath, ".data-protection", "development-keys");
    Directory.CreateDirectory(developmentKeyPath);
    builder.Services.Configure<KeyManagementOptions>(options =>
        options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(developmentKeyPath), NullLoggerFactory.Instance));
}

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    // In Development the sample-data demo stays open so the UI can be reviewed without an account.
    // Everywhere else the portals require a signed-in user with the right role.
    var demoOpen = builder.Environment.IsDevelopment() && builder.Configuration.GetValue("Demo:Enabled", true);
    if (!demoOpen)
    {
        options.Conventions.AuthorizeFolder("/Customer", Endpoints.CustomerPolicy);
        options.Conventions.AuthorizeFolder("/Admin", Endpoints.AdminPolicy);
        options.Conventions.AllowAnonymousToPage("/Admin/Login");
    }
});
builder.AddBackend();
builder.Services.AddSingleton<PlanCatalog>();
builder.Services.AddSingleton<IFormTestingMode, FormTestingMode>();
builder.Services.AddSingleton<IDemoMode, DemoMode>();
builder.Services.AddSingleton<IAdminPortalProjection, AdminPortalProjection>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

await app.InitialiseBackendAsync();

app.MapBackend();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
