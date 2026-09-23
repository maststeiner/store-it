using Microsoft.AspNetCore.Http.HttpResults;
using StoreIt.Application;

namespace StoreIt.Api;

/// <summary>
/// SPEC-007 sharing endpoints under <c>/api/v1</c>: the storage's invitation link, its members,
/// leaving, and redeeming a token. Same group rules as the storages tree (session + CSRF).
/// Tokens are accepted in request bodies only (D3 / EC-10).
/// </summary>
public static class SharingEndpoints
{
    public static IEndpointRouteBuilder MapSharingEndpointsV1(this IEndpointRouteBuilder app)
    {
        var storage = app.MapGroup("/api/v1/storages/{storageId}")
            .WithTags("Sharing")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(CsrfEndpointFilter.Validate);

        MapGetInvitation(storage);
        MapCreateInvitation(storage);
        MapDeactivateInvitation(storage);
        MapGetMembers(storage);
        MapRemoveMember(storage);
        MapLeaveStorage(storage);

        var invitations = app.MapGroup("/api/v1/invitations")
            .WithTags("Sharing")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter(CsrfEndpointFilter.Validate);

        MapPreviewInvitation(invitations);
        MapAcceptInvitation(invitations);

        return app;
    }

    private static void MapGetInvitation(RouteGroupBuilder group)
    {
        group
            .MapGet(
                "/invitation",
                async Task<Results<Ok<InvitationStatusResponse>, ProblemHttpResult>> (
                    string storageId,
                    GetInvitationUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (
                        !StorageEndpoints.TryParseRouteId(
                            storageId,
                            nameof(storageId),
                            out var id,
                            out var problem
                        )
                    )
                    {
                        return problem;
                    }

                    return TypedResults.Ok(
                        InvitationStatusResponse.From(await useCase.ExecuteAsync(id, ct))
                    );
                }
            )
            .WithName("getInvitation")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static void MapCreateInvitation(RouteGroupBuilder group)
    {
        group
            .MapPost(
                "/invitation",
                async Task<Results<Ok<CreatedInvitationResponse>, ProblemHttpResult>> (
                    string storageId,
                    CreateInvitationUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (
                        !StorageEndpoints.TryParseRouteId(
                            storageId,
                            nameof(storageId),
                            out var id,
                            out var problem
                        )
                    )
                    {
                        return problem;
                    }

                    return TypedResults.Ok(
                        CreatedInvitationResponse.From(await useCase.ExecuteAsync(id, ct))
                    );
                }
            )
            .WithName("createInvitation")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static void MapDeactivateInvitation(RouteGroupBuilder group)
    {
        group
            .MapDelete(
                "/invitation",
                async Task<Results<NoContent, ProblemHttpResult>> (
                    string storageId,
                    DeactivateInvitationUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (
                        !StorageEndpoints.TryParseRouteId(
                            storageId,
                            nameof(storageId),
                            out var id,
                            out var problem
                        )
                    )
                    {
                        return problem;
                    }

                    await useCase.ExecuteAsync(id, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("deactivateInvitation")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static void MapGetMembers(RouteGroupBuilder group)
    {
        group
            .MapGet(
                "/members",
                async Task<Results<Ok<IEnumerable<StorageMemberResponse>>, ProblemHttpResult>> (
                    string storageId,
                    ListMembersUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (
                        !StorageEndpoints.TryParseRouteId(
                            storageId,
                            nameof(storageId),
                            out var id,
                            out var problem
                        )
                    )
                    {
                        return problem;
                    }

                    var members = await useCase.ExecuteAsync(id, ct);
                    return TypedResults.Ok(members.Select(StorageMemberResponse.From));
                }
            )
            .WithName("getMembers")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static void MapRemoveMember(RouteGroupBuilder group)
    {
        group
            .MapDelete(
                "/members/{userId}",
                async Task<Results<NoContent, ProblemHttpResult>> (
                    string storageId,
                    string userId,
                    RemoveMemberUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (
                        !StorageEndpoints.TryParseRouteId(
                            storageId,
                            nameof(storageId),
                            out var id,
                            out var problem
                        )
                    )
                    {
                        return problem;
                    }
                    if (
                        !StorageEndpoints.TryParseRouteId(
                            userId,
                            nameof(userId),
                            out var memberId,
                            out var memberProblem
                        )
                    )
                    {
                        return memberProblem;
                    }

                    await useCase.ExecuteAsync(id, memberId, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("removeMember")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static void MapLeaveStorage(RouteGroupBuilder group)
    {
        group
            .MapDelete(
                "/membership",
                async Task<Results<NoContent, ProblemHttpResult>> (
                    string storageId,
                    LeaveStorageUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (
                        !StorageEndpoints.TryParseRouteId(
                            storageId,
                            nameof(storageId),
                            out var id,
                            out var problem
                        )
                    )
                    {
                        return problem;
                    }

                    await useCase.ExecuteAsync(id, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("leaveStorage")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapPreviewInvitation(RouteGroupBuilder group)
    {
        group
            .MapPost(
                "/preview",
                async Task<Ok<InvitationPreviewResponse>> (
                    InvitationTokenRequest request,
                    PreviewInvitationUseCase useCase,
                    CancellationToken ct
                ) =>
                    TypedResults.Ok(
                        InvitationPreviewResponse.From(
                            await useCase.ExecuteAsync(request.Token, ct)
                        )
                    )
            )
            .WithName("previewInvitation")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static void MapAcceptInvitation(RouteGroupBuilder group)
    {
        group
            .MapPost(
                "/accept",
                async Task<Ok<AcceptedInvitationResponse>> (
                    InvitationTokenRequest request,
                    AcceptInvitationUseCase useCase,
                    CancellationToken ct
                ) =>
                    TypedResults.Ok(
                        new AcceptedInvitationResponse(
                            await useCase.ExecuteAsync(request.Token, ct)
                        )
                    )
            )
            .WithName("acceptInvitation")
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
