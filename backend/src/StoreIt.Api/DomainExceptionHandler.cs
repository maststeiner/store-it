using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Api;

/// <summary>
/// Maps domain/application exceptions to RFC-7807 ProblemDetails with locale-neutral
/// error codes (clients translate; arc42 §8). No internal details leak to clients.
/// </summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    /// <summary>
    /// Exceptions whose status and error code are fixed per type (the message is the detail).
    /// Kept as data rather than switch arms so the handler stays flat as features add cases.
    /// </summary>
    private static readonly Dictionary<Type, (int status, string errorCode)> ByType = new()
    {
        [typeof(StorageNotFoundException)] = (StatusCodes.Status404NotFound, "storage.notFound"),
        [typeof(ItemNotFoundException)] = (StatusCodes.Status404NotFound, "item.notFound"),
        // SPEC-007 sharing
        [typeof(StorageOwnerOnlyException)] = (StatusCodes.Status403Forbidden, "storage.ownerOnly"),
        [typeof(InvitationInvalidException)] = (StatusCodes.Status404NotFound, "invite.invalid"),
        [typeof(MemberNotFoundException)] = (StatusCodes.Status404NotFound, "member.notFound"),
        [typeof(OwnerCannotLeaveException)] = (
            StatusCodes.Status409Conflict,
            "storage.ownerCannotLeave"
        ),
        // SPEC-006 AC-05: the session belongs to an account deleted from another device —
        // end it here instead of answering 500 on the FK violation (sign-out below).
        [typeof(OwnerNoLongerExistsException)] = (
            StatusCodes.Status401Unauthorized,
            "auth.session.stale"
        ),
    };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        (int status, string errorCode, string detail)? mapping = exception switch
        {
            DomainValidationException validation => (
                StatusCodes.Status400BadRequest,
                validation.ErrorCode,
                validation.Message
            ),
            // Malformed request bodies (e.g. unit outside the fixed enum list) are
            // client errors, not server errors (AC-06)
            BadHttpRequestException badRequest => (
                badRequest.StatusCode,
                "request.invalid",
                "The request body is invalid."
            ),
            _ when ByType.TryGetValue(exception.GetType(), out var fixedMapping) => (
                fixedMapping.status,
                fixedMapping.errorCode,
                exception.Message
            ),
            _ => null,
        };

        if (mapping is null)
        {
            return false;
        }

        if (exception is OwnerNoLongerExistsException)
        {
            await httpContext.SignOutAsync(AuthenticationSetup.CookieScheme);
        }

        httpContext.Response.StatusCode = mapping.Value.status;

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = mapping.Value.status,
                    Title = mapping.Value.errorCode,
                    Detail = mapping.Value.detail,
                    Extensions = { ["errorCode"] = mapping.Value.errorCode },
                },
            }
        );
    }
}
