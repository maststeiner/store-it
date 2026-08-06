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
    /// A POST with a structurally-valid X-XSRF-TOKEN that belongs to a DIFFERENT
    /// antiforgery session (different cookie) must be rejected with 403 Forbidden.
    /// This exercises the true mismatch path — the token parses fine but does not match
    /// this session's cookie — distinct from the missing-token test, which fails earlier
    /// at token parsing. A well-formed foreign token is obtained from a second,
    /// independent GET /auth/csrf.
    /// </summary>
    [Fact]
    public async Task Post_WithMismatchedCsrfToken_Returns403()
    {
        // Session A: obtain its own antiforgery cookie via /auth/csrf, but keep A's own
        // header token unset — we will attach a foreign one below.
        var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Test-Subject", "csrf-test-mismatch-a");
        await clientA.GetAsync("/auth/csrf");

        // Session B: a completely independent client → a different antiforgery cookie and
        // thus a different-but-well-formed request token.
        var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Test-Subject", "csrf-test-mismatch-b");
        var foreignToken = await GetXsrfTokenAsync(clientB);

        // Attach B's valid token to A's request: it parses, but validates against A's
        // cookie and mismatches → 403 (a true CSRF rejection, not a parse failure).
        clientA.DefaultRequestHeaders.Add("X-XSRF-TOKEN", foreignToken);

        var response = await clientA.PostAsJsonAsync("/api/v1/storages", new { name = "Vault" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Calls GET /auth/csrf on <paramref name="client"/> and returns the JS-readable
    /// XSRF-TOKEN request token from the Set-Cookie header.
    /// </summary>
    private static async Task<string> GetXsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/auth/csrf");
        response.EnsureSuccessStatusCode();

        Assert.True(
            response.Headers.TryGetValues("Set-Cookie", out var setCookies),
            "GET /auth/csrf did not set any cookie."
        );
        var xsrfCookieLine = setCookies.FirstOrDefault(c =>
            c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal)
        );
        Assert.NotNull(xsrfCookieLine);

        // Cookie line format: "XSRF-TOKEN=<value>; path=/; ..."
        return xsrfCookieLine.Split(';')[0].Split('=', 2)[1];
    }
}
