using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http.HttpResults;
using StoreIt.Application;

namespace StoreIt.Api;

public static class StorageEndpoints
{
    /// <summary>
    /// Locale-neutral error code for a route id that is not a GUID (arc42 §8).
    /// </summary>
    private const string InvalidRouteIdErrorCode = "request.invalidId";

    /// <summary>
    /// SPEC-001 endpoints under /api/v1 (ADR-006 URL versioning).
    /// Handlers return typed results so the OpenAPI contract captures response
    /// shapes and status codes — the basis for the drift + breaking-change gate.
    /// Stable .WithName(...) operationIds keep generated client method names clean
    /// (SPEC-002).
    /// Route ids bind as strings and are parsed explicitly (see
    /// <see cref="TryParseRouteId"/>) so a malformed id answers 400 API-wide.
    /// The two route groups are mapped by one method each: the per-endpoint id
    /// guards add up, and one method for all nine endpoints exceeded the
    /// cognitive-complexity budget (SonarCloud S3776).
    /// </summary>
    public static IEndpointRouteBuilder MapStorageEndpointsV1(this IEndpointRouteBuilder app)
    {
        var storages = app.MapGroup("/api/v1/storages").WithTags("Storages");

        MapStorageRoutes(storages);
        MapItemRoutes(storages);

        return app;
    }

    /// <summary>
    /// Collection and single-storage routes under /api/v1/storages.
    /// </summary>
    private static void MapStorageRoutes(RouteGroupBuilder storages)
    {
        storages
            .MapGet(
                "/",
                async Task<Ok<IEnumerable<StorageResponse>>> (
                    ListStoragesUseCase useCase,
                    CancellationToken ct
                ) => TypedResults.Ok((await useCase.ExecuteAsync(ct)).Select(StorageResponse.From))
            )
            .WithName("getStorages");

        storages
            .MapGet(
                "/{storageId}",
                async Task<Results<Ok<StorageResponse>, ProblemHttpResult>> (
                    string storageId,
                    GetStorageUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (!TryParseRouteId(storageId, nameof(storageId), out var id, out var problem))
                    {
                        return problem;
                    }

                    return TypedResults.Ok(
                        StorageResponse.From(await useCase.ExecuteAsync(id, ct))
                    );
                }
            )
            .WithName("getStorage")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        storages
            .MapPost(
                "/",
                async Task<Created<StorageResponse>> (
                    StorageRequest request,
                    CreateStorageUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    var summary = await useCase.ExecuteAsync(request.Name, ct);
                    return TypedResults.Created(
                        $"/api/v1/storages/{summary.Id}",
                        StorageResponse.From(summary)
                    );
                }
            )
            .WithName("createStorage")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        storages
            .MapPut(
                "/{storageId}",
                async Task<Results<Ok<StorageResponse>, ProblemHttpResult>> (
                    string storageId,
                    StorageRequest request,
                    RenameStorageUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (!TryParseRouteId(storageId, nameof(storageId), out var id, out var problem))
                    {
                        return problem;
                    }

                    return TypedResults.Ok(
                        StorageResponse.From(await useCase.ExecuteAsync(id, request.Name, ct))
                    );
                }
            )
            .WithName("renameStorage")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        storages
            .MapDelete(
                "/{storageId}",
                async Task<Results<NoContent, ProblemHttpResult>> (
                    string storageId,
                    DeleteStorageUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (!TryParseRouteId(storageId, nameof(storageId), out var id, out var problem))
                    {
                        return problem;
                    }

                    await useCase.ExecuteAsync(id, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("deleteStorage")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Item routes nested under a storage: /api/v1/storages/{storageId}/items.
    /// </summary>
    private static void MapItemRoutes(RouteGroupBuilder storages)
    {
        var items = storages.MapGroup("/{storageId}/items").WithTags("Items");

        items
            .MapGet(
                "/",
                async Task<Results<Ok<IEnumerable<ItemResponse>>, ProblemHttpResult>> (
                    string storageId,
                    GetStorageItemsUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (!TryParseRouteId(storageId, nameof(storageId), out var id, out var problem))
                    {
                        return problem;
                    }

                    return TypedResults.Ok(
                        (await useCase.ExecuteAsync(id, ct)).Select(ItemResponse.From)
                    );
                }
            )
            .WithName("getItems")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        items
            .MapPost(
                "/",
                async Task<Results<Created<Guid>, ProblemHttpResult>> (
                    string storageId,
                    ItemRequest request,
                    AddItemUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (!TryParseRouteId(storageId, nameof(storageId), out var id, out var problem))
                    {
                        return problem;
                    }

                    var item = await useCase.ExecuteAsync(
                        new AddItemInput(
                            id,
                            request.Name,
                            request.Amount,
                            request.Unit,
                            request.ExpiryDate,
                            request.ProductionDate
                        ),
                        ct
                    );
                    return TypedResults.Created($"/api/v1/storages/{id}/items/{item.Id}", item.Id);
                }
            )
            .WithName("addItem")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        items
            .MapPut(
                "/{itemId}",
                async Task<Results<NoContent, ProblemHttpResult>> (
                    string storageId,
                    string itemId,
                    ItemRequest request,
                    UpdateItemUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (
                        !TryParseRouteId(
                            storageId,
                            nameof(storageId),
                            out var parsedStorageId,
                            out var storageIdProblem
                        )
                    )
                    {
                        return storageIdProblem;
                    }

                    if (
                        !TryParseRouteId(
                            itemId,
                            nameof(itemId),
                            out var parsedItemId,
                            out var itemIdProblem
                        )
                    )
                    {
                        return itemIdProblem;
                    }

                    // AC-08: amount 0 removes the item — both outcomes are 204
                    await useCase.ExecuteAsync(
                        new UpdateItemInput(
                            parsedStorageId,
                            parsedItemId,
                            request.Name,
                            request.Amount,
                            request.Unit,
                            request.ExpiryDate,
                            request.ProductionDate
                        ),
                        ct
                    );
                    return TypedResults.NoContent();
                }
            )
            .WithName("updateItem")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        items
            .MapDelete(
                "/{itemId}",
                async Task<Results<NoContent, ProblemHttpResult>> (
                    string storageId,
                    string itemId,
                    DeleteItemUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    if (
                        !TryParseRouteId(
                            storageId,
                            nameof(storageId),
                            out var parsedStorageId,
                            out var storageIdProblem
                        )
                    )
                    {
                        return storageIdProblem;
                    }

                    if (
                        !TryParseRouteId(
                            itemId,
                            nameof(itemId),
                            out var parsedItemId,
                            out var itemIdProblem
                        )
                    )
                    {
                        return itemIdProblem;
                    }

                    await useCase.ExecuteAsync(parsedStorageId, parsedItemId, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("deleteItem")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Parses a GUID route value. Ids bind as strings instead of carrying the
    /// <c>:guid</c> route constraint: with the constraint a malformed id left the route
    /// unmatched and surfaced as 404, so the same client error answered differently per
    /// endpoint (issue #69). Parsing explicitly answers 400 ProblemDetails API-wide and
    /// independently of the hosting environment. The raw value is never echoed back.
    /// </summary>
    private static bool TryParseRouteId(
        string value,
        string parameterName,
        out Guid id,
        [NotNullWhen(false)] out ProblemHttpResult? problem
    )
    {
        if (Guid.TryParse(value, out id))
        {
            problem = null;
            return true;
        }

        problem = TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: InvalidRouteIdErrorCode,
            detail: $"'{parameterName}' must be a GUID.",
            extensions: new Dictionary<string, object?> { ["errorCode"] = InvalidRouteIdErrorCode }
        );
        return false;
    }
}
