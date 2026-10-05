using Microsoft.AspNetCore.Http.HttpResults;
using StoreIt.Application;

namespace StoreIt.Api;

/// <summary>
/// SPEC-008: the operator's read-only view under <c>/api/v1/admin</c>. The group requires
/// the <c>Admin</c> policy (D2): no session → 401, a signed-in non-admin → 403. GET only,
/// so no CSRF filter.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpointsV1(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization(AdminAccess.PolicyName)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // AC-01 / AC-03…AC-06: aggregates only, computed on request (D3/D4).
        admin
            .MapGet(
                "/statistics",
                async Task<Ok<UsageStatisticsResponse>> (
                    GetUsageStatisticsUseCase useCase,
                    CancellationToken ct
                ) => TypedResults.Ok(UsageStatisticsResponse.From(await useCase.ExecuteAsync(ct)))
            )
            .WithName("getUsageStatistics");

        return app;
    }
}
