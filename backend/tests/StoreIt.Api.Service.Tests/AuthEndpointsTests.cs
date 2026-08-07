using System.Net;
using System.Net.Http.Json;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// Service tests for the SPEC-003 auth endpoints — black-box over HTTP against the
/// real API + PostgreSQL (ApiTestFixture). Authentication is exercised via the "Test"
/// scheme (X-Test-* headers), which provisions the user like production's callback.
/// </summary>
public class AuthEndpointsTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    private sealed record MeResponse(string? Id, string? Email, string? Name);

    [Fact]
    public async Task Me_Anonymous_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_Authenticated_ReturnsProfile()
    {
        var client = factory.CreateClientAs(
            subject: "auth-me-subject",
            issuer: "https://test.local",
            email: "pantry.user@example.test",
            name: "Pantry User"
        );

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(profile);
        Assert.Equal("pantry.user@example.test", profile.Email);
        Assert.Equal("Pantry User", profile.Name);
        // sub_local was stamped by provisioning — it must be a real user id.
        Assert.True(Guid.TryParse(profile.Id, out var id) && id != Guid.Empty);
    }

    [Fact]
    public async Task Health_Anonymous_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutCsrfToken_Returns403()
    {
        // A client with no CSRF priming: no antiforgery cookie and no X-XSRF-TOKEN header.
        var client = factory.CreateClient();

        var response = await client.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // SPEC-003 Task 18 — dev-login (Development-only E2E shortcut)
    // -------------------------------------------------------------------------

    /// <summary>
    /// POST /auth/dev-login must return 204 and set a session cookie in
    /// Development mode (the only environment used by WebApplicationFactory).
    /// The resulting session must authorise GET /api/v1/storages → 200, which
    /// proves the full provisioning + sub_local claim path is exercised.
    /// </summary>
    [Fact]
    public async Task DevLogin_Development_Returns204AndSessionAuthorises()
    {
        // Use a plain client — no X-Test-* headers; dev-login establishes the session itself.
        var client = factory.CreateClient();

        // Act — establish the session.
        var loginResponse = await client.PostAsync("/auth/dev-login", null);

        Assert.Equal(HttpStatusCode.NoContent, loginResponse.StatusCode);

        // The session cookie must have been issued.  HttpClient's CookieContainer
        // stores it automatically, so the next request carries it.
        var storagesResponse = await client.GetAsync("/api/v1/storages");
        Assert.Equal(HttpStatusCode.OK, storagesResponse.StatusCode);
    }
}
