using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using StoreIt.Application;

namespace StoreIt.Api;

/// <summary>
/// SPEC-006: the signed-in user's account under <c>/api/v1/account</c>. Same rules as the
/// storages tree: authenticated session (401) and CSRF double-submit token (403).
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpointsV1(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/v1/account")
            .WithTags("Account")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .AddEndpointFilter(CsrfEndpointFilter.Validate);

        // AC-01/AC-02: delete the account (storages and items cascade in the database) and
        // end the session in the same response, exactly as POST /auth/logout does — the
        // principal's sub_local would otherwise point at a row that no longer exists (D3).
        account
            .MapDelete(
                "/",
                async Task<NoContent> (
                    HttpContext http,
                    DeleteAccountUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    await useCase.ExecuteAsync(ct);
                    await http.SignOutAsync(AuthenticationSetup.CookieScheme);
                    return TypedResults.NoContent();
                }
            )
            .WithName("deleteAccount");

        // SPEC-007 AC-22: the numbers the deletion dialog warns with (D5).
        account
            .MapGet(
                "/",
                async Task<Ok<AccountSummaryResponse>> (
                    GetAccountSummaryUseCase useCase,
                    CancellationToken ct
                ) => TypedResults.Ok(AccountSummaryResponse.From(await useCase.ExecuteAsync(ct)))
            )
            .WithName("getAccount");

        return app;
    }
}
