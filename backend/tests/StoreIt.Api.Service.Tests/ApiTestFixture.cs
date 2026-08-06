using Microsoft.AspNetCore.Authentication;
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

        // Dummy OIDC config so AddOpenIdConnect can bind at startup even though the
        // committed appsettings ships empty ClientId/secret. Tests never reach the IdP
        // (the "Test" scheme below authenticates), but the host must still boot.
        foreach (var provider in new[] { "Microsoft", "Google" })
        {
            builder.UseSetting(
                $"Authentication:{provider}:Authority",
                "https://login.test.local"
            );
            builder.UseSetting($"Authentication:{provider}:ClientId", "test-client-id");
            builder.UseSetting($"Authentication:{provider}:ClientSecret", "test-client-secret");
            builder.UseSetting(
                $"Authentication:{provider}:CallbackPath",
                $"/auth/callback/{provider.ToLowerInvariant()}"
            );
        }

        builder.ConfigureTestServices(services =>
        {
            // Replace the app's TimeProvider.System with the pinned clock (runs after the
            // app's registrations, so this wins) — makes status-count math deterministic.
            services.AddSingleton(Clock);

            // Make "Test" the default scheme so tests authenticate via request headers
            // (see TestAuthHandler) without a live IdP. Anonymous by default: routes stay
            // open unless a request supplies X-Test-Subject.
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { }
                );
        });
    }

    /// <summary>
    /// A client authenticated as <paramref name="subject"/> via the "Test" scheme,
    /// pre-primed with a valid CSRF token pair so existing mutation tests pass without
    /// any change. The client calls <c>GET /auth/csrf</c> to obtain the double-submit
    /// cookies, then sets the JS-readable token as the default <c>X-XSRF-TOKEN</c>
    /// header. The antiforgery HttpOnly cookie is managed automatically by the
    /// <see cref="HttpClient"/>'s <see cref="System.Net.CookieContainer"/>.
    /// </summary>
    public HttpClient CreateClientAs(
        string subject,
        string? issuer = null,
        string? email = null,
        string? name = null
    )
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        if (issuer is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Issuer", issuer);
        }
        if (email is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Email", email);
        }
        if (name is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Name", name);
        }

        // SPEC-003 (Task 8a): prime the client with a CSRF token pair so mutation
        // requests carry both the antiforgery cookie (auto-managed by the CookieContainer)
        // and the X-XSRF-TOKEN header expected by the endpoint filter.
        PrimeCsrfAsync(client).GetAwaiter().GetResult();

        return client;
    }

    /// <summary>
    /// Calls <c>GET /auth/csrf</c>, reads the <c>XSRF-TOKEN</c> response cookie, and
    /// sets it as the default <c>X-XSRF-TOKEN</c> header on <paramref name="client"/>.
    /// The antiforgery (HttpOnly) cookie is retained automatically by the CookieContainer.
    /// </summary>
    private static async Task PrimeCsrfAsync(HttpClient client)
    {
        var csrfResponse = await client.GetAsync("/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();

        // The XSRF-TOKEN cookie is JS-readable (HttpOnly=false) and holds the request
        // token. Extract it from the Set-Cookie header and echo it as the CSRF header.
        // Fail fast if it is missing: without the token every subsequent mutation would
        // 403, and the failure would surface far from its cause. Assert the priming
        // contract here so a regression in GET /auth/csrf points straight at the source.
        if (
            !csrfResponse.Headers.TryGetValues("Set-Cookie", out var setCookies)
            || setCookies.FirstOrDefault(c =>
                c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal)
            )
                is not { } xsrfCookieLine
        )
        {
            throw new InvalidOperationException(
                "CSRF priming failed: GET /auth/csrf did not set an XSRF-TOKEN cookie. "
                    + "Mutation tests would then fail with unrelated 403s."
            );
        }

        // Cookie line format: "XSRF-TOKEN=<value>; path=/; ..."
        var tokenValue = xsrfCookieLine.Split(';')[0].Split('=', 2)[1];
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", tokenValue);
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
