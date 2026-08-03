using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreIt.Infrastructure;
using Testcontainers.PostgreSql;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// Boots the real API against a real PostgreSQL (Testcontainers) — dev/prod parity,
/// no in-memory substitute (coding guidelines, ADR-003).
/// Local runs use Podman (house standard): see docs/guidelines/test-guidelines.md
/// for the DOCKER_HOST setup; CI uses the runner's Docker daemon as-is.
/// </summary>
public sealed class ApiTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// Deterministic "today" for the whole service-test suite. The fixture pins the
    /// server clock to this date so expiry-relative test data never races the real
    /// calendar (e.g. a run crossing local midnight). Tests derive their dates from
    /// this value instead of <see cref="DateTime.Now"/>.
    /// </summary>
    public static readonly DateOnly Today = new(2026, 6, 15);

    // Pinned at midday UTC to stay clear of day boundaries; LocalTimeZone is forced
    // to UTC so TimeProviderExtensions.Today() resolves to exactly Today above.
    private static readonly TimeProvider Clock = new FixedClock(
        new DateTimeOffset(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)
    );

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(
        "postgres:18-alpine"
    ).Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreItDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:storeit", _postgres.GetConnectionString());
        // Replace the app's TimeProvider.System with the pinned clock (runs after the
        // app's registrations, so this wins) — makes status-count math deterministic.
        builder.ConfigureTestServices(services => services.AddSingleton(Clock));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
