using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// The shape of the OIDC challenge <c>GET /auth/login/{provider}</c> starts (SPEC-003,
/// ADR-004): authorization-code flow with PKCE, the scopes the provisioning callback
/// relies on, and the client id from configuration. Shares
/// <see cref="ForwardedHeadersFixture"/> — a configured Google provider whose discovery
/// document is static, so the challenge never leaves the process.
/// </summary>
public sealed class LoginChallengeTests(ForwardedHeadersFixture factory)
    : IClassFixture<ForwardedHeadersFixture>
{
    [Fact]
    public async Task Login_Google_StartsAuthorizationCodeFlowWithPkce()
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var response = await client.GetAsync("/auth/login/google");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        ChallengeAssertions.AssertAuthorizationCodeChallenge(response.Headers.Location);
    }

    [Fact]
    public async Task Login_ProviderNameIsCaseInsensitive()
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var response = await client.GetAsync("/auth/login/GOOGLE");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        ChallengeAssertions.AssertAuthorizationCodeChallenge(response.Headers.Location);
    }

    [Fact]
    public async Task Login_StaySignedIn_StartsTheSameChallenge()
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // The opt-in rides inside the OIDC state parameter; the authorization request
        // itself must not change (the provider knows nothing about it).
        var response = await client.GetAsync("/auth/login/google?persistent=true");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        ChallengeAssertions.AssertAuthorizationCodeChallenge(response.Headers.Location);
    }

    [Fact]
    public async Task Login_UnsupportedProvider_Returns400ProblemWithErrorCode()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/auth/login/github");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Locale-neutral error code in both places the ProblemDetails contract publishes it.
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("auth.provider.unsupported", problem.GetProperty("title").GetString());
        Assert.Equal("auth.provider.unsupported", problem.GetProperty("errorCode").GetString());
    }
}

/// <summary>
/// The full shape of an authorization-code challenge, asserted by <em>every</em> login test:
/// the OIDC options are built once per host, on the first challenge, so whichever test runs
/// first is the one that exercises <c>ConfigureOidc</c> — each of them has to check all of it.
/// </summary>
internal static class ChallengeAssertions
{
    public static void AssertAuthorizationCodeChallenge(Uri? location)
    {
        Assert.NotNull(location);
        // The static discovery document of the fixture names this endpoint.
        Assert.Equal("https://login.test.local/authorize", location.GetLeftPart(UriPartial.Path));

        var query = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("test-client-id", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.False(string.IsNullOrEmpty(query["state"]));
        var scopes = (query["scope"] ?? string.Empty).Split(' ');
        Assert.Contains("openid", scopes);
        Assert.Contains("email", scopes); // the provisioning callback reads the email claim
        Assert.EndsWith("/auth/callback/google", query["redirect_uri"], StringComparison.Ordinal);
    }
}

/// <summary>
/// What the challenge carries into the session: the "stay signed in" opt-in and the return
/// target. Asserted on the result object — over HTTP both are sealed inside the OIDC
/// <c>state</c> parameter and only come back out in the callback.
/// </summary>
public sealed class LoginChallengePropertiesTests
{
    private static IConfiguration GoogleConfigured() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Authentication:Google:ClientId"] = "test-client-id",
                    ["Authentication:Google:Authority"] = "https://login.test.local",
                }
            )
            .Build();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HandleLogin_CarriesStaySignedInAndReturnTargetIntoTheChallenge(bool persistent)
    {
        var result = AuthEndpoints.HandleLogin(
            "google",
            "/storages/7",
            GoogleConfigured(),
            persistent
        );

        var challenge = Assert.IsType<ChallengeHttpResult>(result);
        Assert.Equal([AuthenticationSetup.GoogleScheme], challenge.AuthenticationSchemes);
        Assert.NotNull(challenge.Properties);
        Assert.Equal(persistent, challenge.Properties.IsPersistent);
        Assert.Equal("/storages/7", challenge.Properties.RedirectUri);
    }

    [Fact]
    public void HandleLogin_DefaultsToABrowserSession()
    {
        var result = AuthEndpoints.HandleLogin("google", null, GoogleConfigured());

        var challenge = Assert.IsType<ChallengeHttpResult>(result);
        Assert.NotNull(challenge.Properties);
        Assert.False(challenge.Properties.IsPersistent);
    }
}

/// <summary>
/// Open-redirect guard behind <c>?returnUrl=</c> (SPEC-003): only an app-local path
/// survives; anything that could leave the origin collapses to <c>/</c>.
/// </summary>
public sealed class SafeReturnUrlTests
{
    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("storages", "/")] // relative, no leading slash
    [InlineData("https://evil.example/", "/")] // absolute
    [InlineData("//evil.example/", "/")] // scheme-relative
    [InlineData("/\\evil.example/", "/")] // backslash-smuggled scheme-relative
    [InlineData("/", "/")]
    [InlineData("/storages/1?tab=expiring", "/storages/1?tab=expiring")]
    public void SafeReturnUrl_KeepsOnlyAppLocalPaths(string? input, string expected)
    {
        Assert.Equal(expected, AuthEndpoints.SafeReturnUrl(input));
    }
}
