using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using StoreIt.Application;
using CookieRedirectContext = Microsoft.AspNetCore.Authentication.RedirectContext<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>;

namespace StoreIt.Api;

/// <summary>
/// Wires the BFF auth stack (SPEC-003): a cookie session as the primary scheme plus
/// per-provider OpenID Connect challenge schemes ("Microsoft", "Google").
/// Provisioning runs once in the OIDC callback (<c>OnTokenValidated</c>) — not per
/// request — so all later requests are read-only claim reads (see <see cref="CurrentUser"/>).
/// </summary>
public static class AuthenticationSetup
{
    /// <summary>Cookie scheme — the authenticated session for the SPA.</summary>
    public const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>OIDC challenge scheme for Microsoft Entra / personal accounts.</summary>
    public const string MicrosoftScheme = "Microsoft";

    /// <summary>OIDC challenge scheme for Google.</summary>
    public const string GoogleScheme = "Google";

    /// <summary>
    /// Returns <see langword="true"/> when a provider section has both a non-empty
    /// <c>ClientId</c> and a non-empty <c>Authority</c>.  Used both when registering
    /// OIDC schemes (startup guard) and when handling <c>/auth/login/{provider}</c>
    /// (runtime unconfigured check), so the two gates stay in sync.
    /// </summary>
    public static bool IsProviderConfigured(IConfiguration config, string providerName) =>
        !string.IsNullOrEmpty(config[$"Authentication:{providerName}:ClientId"])
        && !string.IsNullOrEmpty(config[$"Authentication:{providerName}:Authority"]);

    public static IServiceCollection AddStoreItAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment env
    )
    {
        var builder = services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieScheme;
                // Route bare ChallengeAsync() to the cookie scheme so the
                // FallbackPolicy (RequireAuthenticatedUser) returns 401 — not a
                // redirect to the OIDC authority — for unauthenticated API calls.
                // /auth/login always passes the provider scheme explicitly, so it
                // is unaffected by this default.
                options.DefaultChallengeScheme = CookieScheme;
            })
            .AddCookie(
                CookieScheme,
                options =>
                {
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    // Relax to SameAsRequest in Development so the cookie is sent over
                    // http://localhost. In all other environments the cookie stays Secure.
                    options.Cookie.SecurePolicy = env.IsDevelopment()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
                    // It's an API, not an MVC app: never redirect to a login/denied page.
                    options.Events.OnRedirectToLogin = ReturnStatus(
                        StatusCodes.Status401Unauthorized
                    );
                    options.Events.OnRedirectToAccessDenied = ReturnStatus(
                        StatusCodes.Status403Forbidden
                    );
                }
            );

        // Register each OIDC provider only when BOTH ClientId and Authority are present.
        // A missing ClientId or Authority at startup causes AddOpenIdConnect to throw on
        // every request (including /health) — making app health depend on auth config.
        // With no provider registered the app still boots and serves all non-OIDC
        // endpoints; /auth/login/{provider} uses the same IsProviderConfigured predicate
        // and returns 400 for unconfigured providers.
        if (IsProviderConfigured(configuration, MicrosoftScheme))
        {
            builder.AddOpenIdConnect(
                MicrosoftScheme,
                options =>
                    ConfigureOidc(
                        options,
                        configuration.GetSection("Authentication:Microsoft"),
                        env
                    )
            );
        }

        if (IsProviderConfigured(configuration, GoogleScheme))
        {
            builder.AddOpenIdConnect(
                GoogleScheme,
                options =>
                    ConfigureOidc(options, configuration.GetSection("Authentication:Google"), env)
            );
        }

        // Secure-by-default (SPEC-003 ownership cutover): every endpoint requires an
        // authenticated user unless it opts out with .AllowAnonymous() (the /auth group,
        // /health, and the OpenAPI document — see Program.cs).
        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    private static void ConfigureOidc(
        OpenIdConnectOptions options,
        IConfigurationSection config,
        IWebHostEnvironment env
    )
    {
        // The session lives in the cookie; OIDC only handles the challenge/callback.
        options.SignInScheme = CookieScheme;

        options.Authority = config["Authority"];
        options.ClientId = config["ClientId"];
        options.ClientSecret = config["ClientSecret"];
        var callbackPath = config["CallbackPath"];
        if (!string.IsNullOrEmpty(callbackPath))
        {
            options.CallbackPath = callbackPath;
        }

        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Add("email");
        options.MapInboundClaims = false;

        // Config may be empty at startup (secrets arrive via env in real deploys, and
        // tests never reach the IdP). Defer authority/metadata validation to
        // challenge-time so an empty ClientId cannot fail host startup.
        // RequireHttpsMetadata is relaxed only in Development (local/test); in all
        // other environments it stays true to prevent MITM/downgrade attacks on the
        // OIDC discovery document.
        options.RequireHttpsMetadata = !env.IsDevelopment();

        // Provision-once: resolve and persist the local user during the OIDC callback,
        // then stamp the internal id onto the principal as the sub_local claim.
        options.Events.OnTokenValidated = async ctx =>
        {
            var provision =
                ctx.HttpContext.RequestServices.GetRequiredService<ProvisionUserUseCase>();
            var principal = ctx.Principal!;
            var subject =
                principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub");
            if (subject is null)
            {
                ctx.Fail("No subject claim.");
                return;
            }

            var issuer =
                principal.FindFirstValue("iss")
                ?? (string.IsNullOrEmpty(options.Authority) ? null : options.Authority);
            if (issuer is null)
            {
                ctx.Fail("No issuer: the 'iss' claim is absent and Authority is not configured.");
                return;
            }

            var user = await provision.ExecuteAsync(
                issuer,
                subject,
                principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email"),
                principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name),
                ctx.HttpContext.RequestAborted
            );

            ((ClaimsIdentity)principal.Identity!).AddClaim(
                new Claim(CurrentUser.LocalIdClaim, user.Id.ToString())
            );
        };
    }

    private static Func<CookieRedirectContext, Task> ReturnStatus(int statusCode) =>
        ctx =>
        {
            ctx.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        };
}
