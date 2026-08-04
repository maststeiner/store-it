using System.Security.Claims;
using StoreIt.Application;

namespace StoreIt.Api;

/// <summary>
/// Reads the internal user id from the authenticated cookie principal (SPEC-003).
/// The <see cref="LocalIdClaim"/> is stamped once, during OIDC callback provisioning
/// (<c>OnTokenValidated</c>), so per-request access is a pure claim read — no DB hit.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    /// <summary>Claim carrying the internal (local) user id on the principal.</summary>
    public const string LocalIdClaim = "sub_local";

    public Guid? UserId =>
        Guid.TryParse(
            httpContextAccessor.HttpContext?.User.FindFirstValue(LocalIdClaim),
            out var id
        )
            ? id
            : null;
}
