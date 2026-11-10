using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace HydroSentinel.Data;

/// <summary>EF tooling factory: migrations are generated for PostgreSQL regardless of local SQLite settings.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        // Scaffolding only needs a valid provider configuration. Database update/deployment
        // requires the real connection string to be supplied by the operator.
        if (string.IsNullOrWhiteSpace(connection))
            connection = "Host=localhost;Database=hydrosentinel";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(NormalizePostgresConnectionString(connection), npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Render exposes PostgreSQL credentials as a postgresql:// URI; Npgsql expects key/value syntax.</summary>
    public static string NormalizePostgresConnectionString(string connection)
    {
        if (!Uri.TryCreate(connection, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
            return connection;

        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = credentials.Length > 0 ? Uri.UnescapeDataString(credentials[0]) : string.Empty,
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty
        };

        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2)
            {
                var key = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
                var value = Uri.UnescapeDataString(pair[1].Replace('+', ' '));
                if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                    builder.SslMode = Enum.Parse<SslMode>(value, ignoreCase: true);
                else
                    builder[key] = value;
            }
        }

        return builder.ConnectionString;
    }
}
