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
    public StoreItDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StoreItDbContext>()
            // A placeholder connection string: migration scaffolding needs a configured
            // provider, not a live database.
            .UseNpgsql("Host=localhost;Database=storeit;Username=storeit;Password=storeit")
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
