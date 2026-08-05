using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreIt.Infrastructure;
using Testcontainers.PostgreSql;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// Regression test: the app must boot and serve /health + unauthenticated endpoints
/// even when OIDC providers are NOT configured (empty ClientId/Secret — the default
/// committed appsettings.json). This proves the runtime 500-on-every-request bug
/// (ArgumentException: ClientId cannot be empty) is fixed.
///
/// Fixture intentionally does NOT inject any Authentication:*:ClientId settings,
/// mirroring exactly the committed appsettings.json defaults.
/// </summary>
public sealed class NoOidcConfigTests : IClassFixture<NoOidcApiFixture>
{
    private readonly NoOidcApiFixture _factory;

    public NoOidcConfigTests(NoOidcApiFixture factory) => _factory = factory;

    [Fact]
    public async Task Health_WithNoOidcConfig_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Storages_AnonymousWithNoOidcConfig_Returns401NotServerError()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/storages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnconfiguredProvider_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/auth/login/microsoft");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("auth.provider.unconfigured", body, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Boots the real API against a real PostgreSQL without any OIDC config —
/// ClientId and ClientSecret are intentionally left empty (not set at all).
/// Uses the same "Test" auth scheme as <see cref="ApiTestFixture"/> so the app
/// serves requests, but no dummy OIDC settings are injected.
/// </summary>
public sealed class NoOidcApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
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

        // Intentionally NO Authentication:*:ClientId settings — this is the
        // exact scenario that regressed (empty OIDC config from appsettings.json).

        builder.ConfigureTestServices(services =>
        {
            // Replace the default scheme so test requests can authenticate via
            // X-Test-Subject without a live IdP — same pattern as ApiTestFixture.
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { }
                );
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
