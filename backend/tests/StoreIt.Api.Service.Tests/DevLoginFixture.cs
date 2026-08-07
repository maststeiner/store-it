using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreIt.Infrastructure;
using Testcontainers.PostgreSql;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// Boots the real API in the Development environment with the real cookie + OIDC pipeline —
/// NO "Test" scheme override. Required to test POST /auth/dev-login, which is mapped only
/// when <c>app.Environment.IsDevelopment()</c> and must sign in via the real cookie scheme.
///
/// Dummy OIDC config is injected so AddOpenIdConnect can bind at startup (same technique
/// as <see cref="ApiTestFixture"/>); tests never reach the IdP because dev-login bypasses
/// the OIDC flow entirely.
///
/// Cookie.SecurePolicy is SameAsRequest in Development, so the session cookie round-trips
/// over the plain-HTTP test transport without being dropped.
/// </summary>
public sealed class DevLoginFixture : WebApplicationFactory<Program>, IAsyncLifetime
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
        // Boot as Development so Program.cs maps MapDevAuthEndpoints() and the cookie
        // SecurePolicy is SameAsRequest (http-friendly for the test transport).
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:storeit", _postgres.GetConnectionString());

        // Dummy OIDC config — same trick as ApiTestFixture.  The host must be able to
        // bind the OpenIdConnect options even though tests never reach the IdP.
        foreach (var provider in new[] { "Microsoft", "Google" })
        {
            builder.UseSetting($"Authentication:{provider}:Authority", "https://login.test.local");
            builder.UseSetting($"Authentication:{provider}:ClientId", "test-client-id");
            builder.UseSetting($"Authentication:{provider}:ClientSecret", "test-client-secret");
            builder.UseSetting(
                $"Authentication:{provider}:CallbackPath",
                $"/auth/callback/{provider.ToLowerInvariant()}"
            );
        }

        // No ConfigureTestServices / No auth-scheme override — the real cookie pipeline must run.
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
