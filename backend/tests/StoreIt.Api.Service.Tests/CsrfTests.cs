using System.Net;
using System.Net.Http.Json;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// Service tests for SPEC-003 Task 8a: double-submit CSRF protection on
/// <c>/api/v1/**</c> mutation endpoints (POST/PUT/DELETE).
/// </summary>
public class CsrfTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    /// <summary>
    /// A POST without an X-XSRF-TOKEN header (no CSRF token at all) must be rejected
    /// with 403 Forbidden, even for an authenticated user.
    /// </summary>
    [Fact]
    public async Task Post_WithoutCsrfToken_Returns403()
    {
        // Use a raw (non-primed) authenticated client: auth headers are set but no
        // CSRF cookie/header pair is obtained, so the endpoint filter must reject it.
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", "csrf-test-no-token");

        var response = await client.PostAsJsonAsync("/api/v1/storages", new { name = "Vault" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// A POST with a syntactically valid X-XSRF-TOKEN that does NOT match the
    /// antiforgery cookie must be rejected with 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task Post_WithMismatchedCsrfToken_Returns403()
    {
        // Obtain a real antiforgery cookie via /auth/csrf, then override the header
        // with a different (mismatched) token value — simulating a CSRF attack where
        // the attacker guesses/forges the header but cannot read the HttpOnly cookie.
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", "csrf-test-mismatch");

        // Prime only the cookies (call /auth/csrf) but supply a wrong header value.
        await client.GetAsync("/auth/csrf");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "not-the-real-token");

        var response = await client.PostAsJsonAsync("/api/v1/storages", new { name = "Vault" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
