using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StoreIt.Application;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// The exception → ProblemDetails mapping for cases the HTTP tests cannot provoke
/// deterministically — SPEC-011: a tag write conflict between two members is a 409 with a
/// locale-neutral code, never a 500.
/// </summary>
public sealed class DomainExceptionHandlerTests
{
    [Fact]
    public async Task TagConflict_MapsTo409WithErrorCode()
    {
        var sink = new CapturingProblemDetailsService();
        var handler = new DomainExceptionHandler(sink);
        var http = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            http,
            new TagConflictException(),
            CancellationToken.None
        );

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, http.Response.StatusCode);
        Assert.Equal("item.tags.conflict", sink.Written?.Extensions["errorCode"]);
    }

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }
    }
}
