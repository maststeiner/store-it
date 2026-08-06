using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StoreIt.Api;

/// <summary>
/// SPEC-003 contract completeness: documents the BFF security model in the published
/// OpenAPI so generated clients know (a) which operations require an authenticated
/// session and (b) that mutations must carry the CSRF header.
///
/// <para>
/// Declares a <c>components.securitySchemes</c> entry for the cookie session (apiKey in
/// the <c>.AspNetCore.Cookies</c> cookie) and applies it as a <c>security</c> requirement
/// to every protected operation — everything under <c>/api/v1/**</c> plus <c>/auth/me</c>.
/// The public endpoints (<c>/health</c>, <c>/auth/login|callback|logout|csrf</c>) stay
/// unauthenticated and carry no requirement.
/// </para>
///
/// <para>
/// For mutating operations (POST/PUT/DELETE) under <c>/api/v1/**</c> it also declares the
/// required <c>X-XSRF-TOKEN</c> header parameter (double-submit CSRF token obtained via
/// <c>GET /auth/csrf</c>), so a generated client knows to send it.
/// </para>
///
/// Contract-only; runtime behaviour is unchanged (the auth fallback policy and the CSRF
/// endpoint filter remain the source of truth for enforcement).
/// </summary>
internal sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    internal const string CookieSessionSchemeName = "cookieSession";
    internal const string SessionCookieName = ".AspNetCore.Cookies";
    internal const string CsrfHeaderName = "X-XSRF-TOKEN";

    private static readonly string[] MutatingMethods = ["POST", "PUT", "DELETE", "PATCH"];

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = SessionCookieName,
            Description =
                "BFF session cookie (HttpOnly). Established by the OIDC login flow "
                + "(GET /auth/login/{provider} → callback).",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[CookieSessionSchemeName] = scheme;

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(CookieSessionSchemeName, document)] = [],
        };

        foreach (var (path, item) in document.Paths ?? [])
        {
            if (item.Operations is null)
            {
                continue;
            }

            var isProtected = IsProtectedPath(path);
            var isApiV1 = path.StartsWith("/api/v1/", StringComparison.Ordinal);

            foreach (var (method, operation) in item.Operations)
            {
                if (isProtected)
                {
                    operation.Security ??= [];
                    operation.Security.Add(requirement);
                }

                if (isApiV1 && IsMutating(method))
                {
                    AddCsrfHeader(operation);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Protected = requires an authenticated session: the whole storages tree plus
    /// <c>/auth/me</c>. The other <c>/auth/*</c> endpoints and <c>/health</c> are public.
    /// </summary>
    private static bool IsProtectedPath(string path) =>
        path.StartsWith("/api/v1/", StringComparison.Ordinal)
        || path.Equals("/auth/me", StringComparison.Ordinal);

    private static bool IsMutating(HttpMethod method) =>
        MutatingMethods.Contains(method.Method, StringComparer.OrdinalIgnoreCase);

    private static void AddCsrfHeader(OpenApiOperation operation)
    {
        operation.Parameters ??= [];
        if (
            operation.Parameters.Any(p =>
                string.Equals(p.Name, CsrfHeaderName, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return;
        }

        operation.Parameters.Add(
            new OpenApiParameter
            {
                Name = CsrfHeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description =
                    "Double-submit CSRF token. Obtain it from GET /auth/csrf (which sets "
                    + "the JS-readable XSRF-TOKEN cookie) and echo the value here.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            }
        );
    }
}
