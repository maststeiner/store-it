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
            StorageNotFoundException => (
                StatusCodes.Status404NotFound,
                "storage.notFound",
                exception.Message
            ),
            ItemNotFoundException => (
                StatusCodes.Status404NotFound,
                "item.notFound",
                exception.Message
            ),
            // Malformed request bodies (e.g. unit outside the fixed enum list) are
            // client errors, not server errors (AC-06)
            BadHttpRequestException badRequest => (
                badRequest.StatusCode,
                "request.invalid",
                "The request body is invalid."
            ),
            _ => null,
        };

        if (mapping is null)
        {
            return false;
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
