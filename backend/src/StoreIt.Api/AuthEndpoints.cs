using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

namespace StoreIt.Api;

/// <summary>
/// BFF auth endpoints (SPEC-003). The whole group is anonymous: login starts an OIDC
/// challenge, logout clears the cookie, and /auth/me reports the current session.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Auth").AllowAnonymous();

        auth.MapGet(
                "/login/{provider}",
                (string provider, string? returnUrl, HttpContext http) =>
                {
                    // Allowlist the provider to a fixed scheme BEFORE challenge — the raw
                    // path segment never reaches the auth system (400 for anything else).
                    var scheme = provider.ToLowerInvariant() switch
                    {
                        "microsoft" => AuthenticationSetup.MicrosoftScheme,
                        "google" => AuthenticationSetup.GoogleScheme,
                        _ => null,
                    };
                    if (scheme is null)
                    {
                        return Results.BadRequest(
                            new { errorCode = "auth.provider.unsupported" }
                        );
                    }

                    return Results.Challenge(
                        new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl) },
                        [scheme]
                    );
                }
            )
            .WithName("login");

        auth.MapPost(
                "/logout",
                async Task<NoContent> (HttpContext http) =>
                {
                    await http.SignOutAsync(AuthenticationSetup.CookieScheme);
                    return TypedResults.NoContent();
                }
            )
            .WithName("logout");

        auth.MapGet(
                "/csrf",
                (IAntiforgery antiforgery, HttpContext http) =>
                {
                    // Generate and store the antiforgery token pair: the HttpOnly cookie
                    // holds the server-side token; we additionally set a JS-readable
                    // XSRF-TOKEN cookie so the SPA can read and echo it as X-XSRF-TOKEN.
                    var tokens = antiforgery.GetAndStoreTokens(http);
                    http.Response.Cookies.Append(
                        "XSRF-TOKEN",
                        tokens.RequestToken!,
                        new CookieOptions
                        {
                            HttpOnly = false,
                            SameSite = SameSiteMode.Lax,
                            Secure = true,
                        }
                    );
                    return Results.NoContent();
                }
            )
            .WithName("csrf");

        auth.MapGet(
                "/me",
                Results<Ok<UserProfileResponse>, UnauthorizedHttpResult> (HttpContext http) =>
                {
                    var user = http.User;
                    if (user.Identity?.IsAuthenticated != true)
                    {
                        return TypedResults.Unauthorized();
                    }

                    return TypedResults.Ok(
                        new UserProfileResponse(
                            user.FindFirstValue(CurrentUser.LocalIdClaim),
                            user.FindFirstValue(ClaimTypes.Email)
                                ?? user.FindFirstValue("email"),
                            user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name)
                        )
                    );
                }
            )
            .WithName("me");

        return app;
    }

    /// <summary>
    /// Open-redirect guard: only accept an app-local path (a single leading slash),
    /// never a scheme-relative or backslash-smuggled absolute URL. Anything else → "/".
    /// </summary>
    internal static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : "/";
}

/// <summary>The authenticated session profile returned by <c>GET /auth/me</c>.</summary>
public sealed record UserProfileResponse(string? Id, string? Email, string? Name);
