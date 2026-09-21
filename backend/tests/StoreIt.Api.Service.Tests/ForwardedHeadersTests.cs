using System.Net;
using System.Web;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Testcontainers.PostgreSql;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-005 AC-09: behind a TLS terminator the API must build absolute URLs — above all
/// the OIDC <c>redirect_uri</c> — from the forwarded scheme, or Google/Microsoft reject
/// the sign-in. Deployments switch this on with <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED=true</c>
/// (the framework's built-in switch, no application code); the test proves the switch does
/// what the runtime contract promises, with the exact headers the proxies send.
/// </summary>
public sealed class ForwardedHeadersTests(ForwardedHeadersFixture factory)
    : IClassFixture<ForwardedHeadersFixture>
{
    private const string PublicHost = "storeit.example.test";

    [Fact]
    public async Task Login_BehindTlsTerminator_BuildsHttpsRedirectUri()
    {
        // Caddy → nginx → API: the proxies preserve the browser's Host header and set
        // X-Forwarded-Proto to the scheme the browser used (runtime contract).
        var response = await ChallengeAsync(forwardedProto: "https");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        ChallengeAssertions.AssertAuthorizationCodeChallenge(response.Headers.Location);
        Assert.Equal(
            $"https://{PublicHost}/auth/callback/google",
            RedirectUriOf(response.Headers.Location)
        );
    }

    [Fact]
    public async Task Login_WithoutForwardedProto_KeepsTheRequestScheme()
    {
        // No proxy in front (the local stack over plain http): the scheme is the request's
        // own. Proves the redirect URI is derived from the request, not hard-coded.
        var response = await ChallengeAsync(forwardedProto: null);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        ChallengeAssertions.AssertAuthorizationCodeChallenge(response.Headers.Location);
        Assert.Equal(
            $"http://{PublicHost}/auth/callback/google",
            RedirectUriOf(response.Headers.Location)
        );
    }

    private async Task<HttpResponseMessage> ChallengeAsync(string? forwardedProto)
    {
        // The challenge answers with a 302 to the authority; follow nothing, inspect it.
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/login/google");
        request.Headers.Host = PublicHost;
        if (forwardedProto is not null)
        {
            request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        }

        return await client.SendAsync(request);
    }

    private static string? RedirectUriOf(Uri? location)
    {
        Assert.NotNull(location);
        return HttpUtility.ParseQueryString(location.Query)["redirect_uri"];
    }
}

/// <summary>
/// Boots the real API the way a deployment behind a TLS terminator runs it:
/// <c>ForwardedHeaders_Enabled=true</c> (what <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c>
/// sets) and a fully configured Google provider. The provider's discovery document is
/// replaced by a static one so the challenge never leaves the process — the test is about
/// the URL the API builds, not about the identity provider.
/// </summary>
public sealed class ForwardedHeadersFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(
        "postgres:18-alpine"
    ).Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:storeit", _postgres.GetConnectionString());

        // The environment variable ASPNETCORE_FORWARDEDHEADERS_ENABLED maps to exactly this
        // configuration key; WebHost.ConfigureWebDefaults reads it and inserts the
        // ForwardedHeaders middleware for X-Forwarded-For and X-Forwarded-Proto.
        builder.UseSetting("ForwardedHeaders_Enabled", "true");

        builder.UseSetting("Authentication:Google:Authority", "https://login.test.local");
        builder.UseSetting("Authentication:Google:ClientId", "test-client-id");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-client-secret");

        builder.ConfigureTestServices(services =>
        {
            // Configure (not PostConfigure): it must run before the handler's own
            // post-configuration, which only creates a metadata-fetching manager when
            // none is set yet.
            services.Configure<OpenIdConnectOptions>(
                AuthenticationSetup.GoogleScheme,
                options =>
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            new OpenIdConnectConfiguration
                            {
                                Issuer = "https://login.test.local",
                                AuthorizationEndpoint = "https://login.test.local/authorize",
                                TokenEndpoint = "https://login.test.local/token",
                            }
                        )
            );
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
