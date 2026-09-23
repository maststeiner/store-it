using Microsoft.AspNetCore.Antiforgery;

namespace StoreIt.Api;

/// <summary>
/// SPEC-003 (Task 8a): CSRF endpoint filter for every authenticated, mutating API group
/// (storages tree, account). Validates the double-submit token (X-XSRF-TOKEN header matched
/// against the HttpOnly antiforgery cookie) for every method EXCEPT the RFC-7231 safe ones
/// (GET/HEAD/OPTIONS/TRACE). Using a safe-method allowlist means a future mutating verb
/// (e.g. PATCH) is protected by default instead of silently skipped. Returns a 403
/// ProblemDetails (matching the groups' <c>.ProducesProblem(403)</c> contract) when the
/// token is missing or invalid.
/// </summary>
internal static class CsrfEndpointFilter
{
    /// <summary>
    /// Locale-neutral error code for a failed CSRF double-submit check (arc42 §8).
    /// </summary>
    internal const string CsrfInvalidErrorCode = "csrf.invalid";

    /// <summary>
    /// RFC-7231 safe HTTP methods: they neither mutate state nor require a CSRF token.
    /// Everything else (POST/PUT/DELETE/PATCH/…) must present a valid double-submit token.
    /// </summary>
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "HEAD",
        "OPTIONS",
        "TRACE",
    };

    internal static async ValueTask<object?> Validate(
        EndpointFilterInvocationContext ctx,
        EndpointFilterDelegate next
    )
    {
        var method = ctx.HttpContext.Request.Method;
        if (!SafeMethods.Contains(method))
        {
            var antiforgery = ctx.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(ctx.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: CsrfInvalidErrorCode,
                    detail: "The anti-forgery (CSRF) token is missing or invalid.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["errorCode"] = CsrfInvalidErrorCode,
                    }
                );
            }
        }

        return await next(ctx);
    }
}
