using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace StoreIt.Api;

/// <summary>
/// SPEC-008 D1: who the operator is. Bound from the <c>Admin</c> configuration section
/// (<c>Admin__Emails</c>, comma-separated). An empty list means no admin exists.
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// E-mail addresses that identify the operator. Compared case-insensitively and trimmed
    /// against the e-mail claim of the signed-in principal (D1).
    /// </summary>
    public string Emails { get; set; } = string.Empty;

    /// <summary>The configured addresses, normalised (trimmed, non-empty).</summary>
    public IReadOnlyList<string> GetEmails() =>
        Emails.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// The one place that decides "is this principal the operator" — used by the
/// <c>Admin</c> policy and by <c>GET /auth/me</c> (<c>isAdmin</c>), so both agree.
/// A pure claim read, no database access (SPEC-003 BFF design).
/// </summary>
public static class AdminAccess
{
    public const string PolicyName = "Admin";

    public static bool IsAdmin(ClaimsPrincipal principal, AdminOptions options)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var normalised = email.Trim();
        return options
            .GetEmails()
            .Any(allowed => string.Equals(allowed, normalised, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Marker requirement behind the <see cref="AdminAccess.PolicyName"/> policy.</summary>
public sealed class AdminRequirement : IAuthorizationRequirement;

/// <summary>Satisfies <see cref="AdminRequirement"/> via <see cref="AdminAccess.IsAdmin"/>.</summary>
public sealed class AdminRequirementHandler(IOptions<AdminOptions> options)
    : AuthorizationHandler<AdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement
    )
    {
        if (AdminAccess.IsAdmin(context.User, options.Value))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Startup diagnostics for the operator configuration (CA1848: source-generated log call).</summary>
public static partial class AdminStartupLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Admin__Emails is not set: no account can open /api/v1/admin (every request answers 403)."
    )]
    public static partial void AdminEmailsNotSet(ILogger logger);
}
