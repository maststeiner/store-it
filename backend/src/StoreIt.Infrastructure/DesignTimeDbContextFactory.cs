using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StoreIt.Application;

namespace StoreIt.Infrastructure;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> (migrations) can build the model without a
/// request scope. It supplies a null-current-user context — the ownership query filter
/// then matches nothing, which is irrelevant at design time (migrations only read the
/// model shape, never execute queries).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StoreItDbContext>
{
    // No credential here by design (avoids a hard-coded secret): `dotnet ef migrations add`
    // only reads the model shape and never connects. Commands that DO connect (e.g.
    // `database update`) require the full connection string — incl. its password — via the
    // `ConnectionStrings__storeit` environment variable (12-factor), read below.
    private const string FallbackConnectionString =
        "Host=localhost;Database=storeit;Username=storeit";

    public StoreItDbContext CreateDbContext(string[] args)
    {
        // Read the connection string from the environment (12-factor: env vars first).
        // The ASP.NET Core convention maps "ConnectionStrings__storeit" → the "storeit"
        // entry. Fall back to a labelled localhost default only when unset (e.g. a fresh
        // dev clone before local secrets are configured, or CI without a live database).
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__storeit")
            ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<StoreItDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StoreItDbContext(options, NullCurrentUser.Instance);
    }

    /// <summary>An unauthenticated current user — no session at design time.</summary>
    private sealed class NullCurrentUser : ICurrentUser
    {
        public static readonly NullCurrentUser Instance = new();

        public Guid? UserId => null;
    }
}
