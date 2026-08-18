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
        // 12-factor: connection string must come from the environment.
        // `dotnet ef migrations add` only reads the model shape and never connects, so the
        // variable may be absent for that command. Commands that DO connect (e.g.
        // `database update`) require the full connection string — incl. its password — via
        // the `ConnectionStrings__storeit` environment variable (set it in your shell or
        // CI secrets before running EF commands that target a real database).
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__storeit")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__storeit for design-time EF operations."
            );

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
