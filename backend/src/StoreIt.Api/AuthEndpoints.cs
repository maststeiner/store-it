using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

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
                (string provider, string? returnUrl, HttpContext http, IConfiguration config) =>
                {
                    // Allowlist the provider to a fixed scheme BEFORE challenge — the raw
                    // path segment never reaches the auth system (400 for anything else).
                    var scheme = provider.ToLowerInvariant() switch
                    {
                        "microsoft" => AuthenticationSetup.MicrosoftScheme,
                        "google"    => AuthenticationSetup.GoogleScheme,
                        _           => null,
                    };
                    if (scheme is null)
                    {
                        return ProviderProblem("auth.provider.unsupported");
                    }

                    // Guard against challenging a scheme that was never registered.
                    // AuthenticationSetup skips OIDC providers whose ClientId or Authority is
                    // empty; ChallengeAsync on a missing scheme would throw. Use the same
                    // IsProviderConfigured predicate so both gates stay in sync.
                    // providerName is always non-null here: the scheme-null branch above
                    // returns early, so the _ arm is unreachable.
                    var providerName = scheme == AuthenticationSetup.MicrosoftScheme
                        ? AuthenticationSetup.MicrosoftScheme
                        : AuthenticationSetup.GoogleScheme;
                    if (!AuthenticationSetup.IsProviderConfigured(config, providerName))
                    {
                        return ProviderProblem("auth.provider.unconfigured");
                    }

                    return Results.Challenge(
                        new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl) },
                        [scheme]
                    );
                }
            )
            .WithName("login")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        auth.MapPost(
                "/logout",
                async Task<Results<NoContent, ProblemHttpResult>> (
                    HttpContext http,
                    IAntiforgery antiforgery
                ) =>
                {
                    try
                    {
                        await antiforgery.ValidateRequestAsync(http);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        return TypedResults.Problem(
                            statusCode: StatusCodes.Status403Forbidden,
                            title: "csrf.invalid",
                            extensions: new Dictionary<string, object?> { ["errorCode"] = "csrf.invalid" }
                        );
                    }

                    await http.SignOutAsync(AuthenticationSetup.CookieScheme);
                    return TypedResults.NoContent();
                }
            )
            .WithName("logout")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        auth.MapGet(
                "/csrf",
                (IAntiforgery antiforgery, HttpContext http, IWebHostEnvironment env) =>
                {
                    // Generate and store the antiforgery token pair: the HttpOnly cookie
                    // holds the server-side token; we additionally set a JS-readable
                    // XSRF-TOKEN cookie so the SPA can read and echo it as X-XSRF-TOKEN.
                    // Secure is relaxed in Development so the cookie is sent over
                    // http://localhost (mirrors the session-cookie SecurePolicy gate).
                    var tokens = antiforgery.GetAndStoreTokens(http);
                    http.Response.Cookies.Append(
                        "XSRF-TOKEN",
                        tokens.RequestToken!,
                        new CookieOptions
                        {
                            HttpOnly = false,
                            SameSite = SameSiteMode.Lax,
                            Secure = !env.IsDevelopment(),
                        }
                    );
                    return Results.NoContent();
                }
            )
            .WithName("csrf")
            .Produces(StatusCodes.Status204NoContent);

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
            .WithName("me")
            .Produces<UserProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    /// <summary>
    /// A 400 ProblemDetails carrying a locale-neutral <paramref name="errorCode"/> (arc42
    /// §8). Matches the app's ProblemDetails/error-code style so the response body conforms
    /// to the endpoint's published <c>.ProducesProblem(400)</c> schema (clients translate).
    /// </summary>
    private static ProblemHttpResult ProviderProblem(string errorCode) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: errorCode,
            extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode }
        );

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
