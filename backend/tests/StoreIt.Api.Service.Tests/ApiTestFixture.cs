using Microsoft.AspNetCore.Mvc.Testing;
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
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(
        "postgres:17-alpine"
    ).Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreItDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(
        Microsoft.AspNetCore.Hosting.IWebHostBuilder builder
    ) => builder.UseSetting("ConnectionStrings:storeit", _postgres.GetConnectionString());

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
