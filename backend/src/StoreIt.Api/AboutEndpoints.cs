using Microsoft.AspNetCore.Http.HttpResults;

namespace StoreIt.Api;

/// <summary>
/// SPEC-009 AC-04: what the running API is — release version, commit, runtime and the
/// third-party packages it ships. Behind the session like every endpoint (D5): the default
/// policy answers 401 without one. GET only, so no CSRF filter.
/// </summary>
public static class AboutEndpoints
{
    public static IEndpointRouteBuilder MapAboutEndpointsV1(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/v1/about")
            .WithTags("About")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .MapGet(
                "/",
                Ok<AboutResponse> (IAboutInformation about) =>
                    TypedResults.Ok(AboutResponse.From(about))
            )
            .WithName("getAbout");

        return app;
    }
}
