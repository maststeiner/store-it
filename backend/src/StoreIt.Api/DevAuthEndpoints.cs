using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using StoreIt.Application;

namespace StoreIt.Api;

/// <summary>
/// Development-only auth endpoint (SPEC-003 Task 18).
///
/// SECURITY NOTE: This class is mapped ONLY when <c>app.Environment.IsDevelopment()</c>
/// (see <see cref="WebApplicationExtensions.MapDevAuthEndpoints"/>). It is intentionally
/// absent in Staging and Production — do not remove the environment guard in Program.cs.
///
/// Purpose: lets E2E (Playwright) tests establish a real cookie session without going
/// through an OIDC provider. The endpoint runs the SAME provisioning + claim contract
/// as production (<see cref="ProvisionUserUseCase"/> + <see cref="CurrentUser.LocalIdClaim"/>)
/// so every authenticated request that follows carries a resolvable <c>sub_local</c>.
/// </summary>
public static class DevAuthEndpoints
{
    /// <summary>Fixed synthetic identity used by all E2E test runs.</summary>
    private const string DevIssuer = "dev";
    private const string DevSubject = "e2e-user";
    private const string DevEmail = "e2e@store-it.local";
    private const string DevName = "E2E Test User";

    /// <summary>
    /// Maps <c>POST /auth/dev-login</c>.
    /// Call only when <c>app.Environment.IsDevelopment()</c> — see Program.cs.
    /// </summary>
    public static IEndpointRouteBuilder MapDevAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Placed under /auth to stay consistent with the production auth group, but
        // registered separately so the production MapAuthEndpoints() is untouched.
        app.MapPost(
                "/auth/dev-login",
                async (
                    ProvisionUserUseCase provision,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    // 1. Provision (find-or-create) the synthetic user — same call as OnTokenValidated.
                    var user = await provision.ExecuteAsync(
                        DevIssuer,
                        DevSubject,
                        DevEmail,
                        DevName,
                        ct
                    );

                    // 2. Build a claims identity that mirrors the production cookie principal,
                    //    including the sub_local claim so ICurrentUser.UserId resolves correctly.
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, DevSubject),
                        new Claim(ClaimTypes.Email, DevEmail),
                        new Claim("name", DevName),
                        new Claim(CurrentUser.LocalIdClaim, user.Id.ToString()),
                    };

                    var identity = new ClaimsIdentity(
                        claims,
                        authenticationType: AuthenticationSetup.CookieScheme
                    );
                    var principal = new ClaimsPrincipal(identity);

                    // 3. Sign in — writes the session cookie to the response.
                    await http.SignInAsync(AuthenticationSetup.CookieScheme, principal);

                    return Results.NoContent();
                }
            )
            .WithTags("Auth")
            .WithName("devLogin")
            .AllowAnonymous()
            // Exempt from CSRF: this endpoint establishes the session, so no token exists yet.
            .DisableAntiforgery()
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
