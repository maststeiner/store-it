using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using StoreIt.Application;
using CookieRedirectContext = Microsoft.AspNetCore.Authentication.RedirectContext<
    Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions
>;

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

    public static IServiceCollection AddStoreItAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieScheme;
                // The default challenge (Microsoft) is only used if a bare
                // ChallengeAsync() is ever called; /auth/login always picks an
                // explicit, allowlisted scheme.
                options.DefaultChallengeScheme = MicrosoftScheme;
            })
            .AddCookie(CookieScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                // It's an API, not an MVC app: never redirect to a login/denied page.
                options.Events.OnRedirectToLogin = ReturnStatus(StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = ReturnStatus(
                    StatusCodes.Status403Forbidden
                );
            })
            .AddOpenIdConnect(
                MicrosoftScheme,
                options => ConfigureOidc(options, configuration.GetSection("Authentication:Microsoft"))
            )
            .AddOpenIdConnect(
                GoogleScheme,
                options => ConfigureOidc(options, configuration.GetSection("Authentication:Google"))
            );

        // Secure-by-default (SPEC-003 ownership cutover): every endpoint requires an
        // authenticated user unless it opts out with .AllowAnonymous() (the /auth group,
        // /health, and the OpenAPI document — see Program.cs).
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build()
        );

        return services;
    }

    private static void ConfigureOidc(OpenIdConnectOptions options, IConfigurationSection config)
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
        options.RequireHttpsMetadata = false;

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

            var user = await provision.ExecuteAsync(
                principal.FindFirstValue("iss") ?? options.Authority!,
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
