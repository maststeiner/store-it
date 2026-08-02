using Microsoft.AspNetCore.Http.HttpResults;
using StoreIt.Application;

namespace StoreIt.Api;

public static class StorageEndpoints
{
    /// <summary>
    /// SPEC-001 endpoints under /api/v1 (ADR-006 URL versioning).
    /// Handlers return typed results so the OpenAPI contract captures response
    /// shapes and status codes — the basis for the drift + breaking-change gate.
    /// Stable .WithName(...) operationIds keep generated client method names clean
    /// (SPEC-002).
    /// </summary>
    public static IEndpointRouteBuilder MapStorageEndpointsV1(this IEndpointRouteBuilder app)
    {
        var storages = app.MapGroup("/api/v1/storages").WithTags("Storages");

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
                "/{storageId:guid}",
                async Task<Ok<StorageResponse>> (
                    Guid storageId,
                    GetStorageUseCase useCase,
                    CancellationToken ct
                ) =>
                    TypedResults.Ok(StorageResponse.From(await useCase.ExecuteAsync(storageId, ct)))
            )
            .WithName("getStorage")
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
                "/{storageId:guid}",
                async Task<Ok<StorageResponse>> (
                    Guid storageId,
                    StorageRequest request,
                    RenameStorageUseCase useCase,
                    CancellationToken ct
                ) =>
                    TypedResults.Ok(
                        StorageResponse.From(
                            await useCase.ExecuteAsync(storageId, request.Name, ct)
                        )
                    )
            )
            .WithName("renameStorage")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        storages
            .MapDelete(
                "/{storageId:guid}",
                async Task<NoContent> (
                    Guid storageId,
                    DeleteStorageUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    await useCase.ExecuteAsync(storageId, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("deleteStorage")
            .ProducesProblem(StatusCodes.Status404NotFound);

        var items = storages.MapGroup("/{storageId:guid}/items").WithTags("Items");

        items
            .MapGet(
                "/",
                async Task<Ok<IEnumerable<ItemResponse>>> (
                    Guid storageId,
                    GetStorageItemsUseCase useCase,
                    CancellationToken ct
                ) =>
                    TypedResults.Ok(
                        (await useCase.ExecuteAsync(storageId, ct)).Select(ItemResponse.From)
                    )
            )
            .WithName("getItems")
            .ProducesProblem(StatusCodes.Status404NotFound);

        items
            .MapPost(
                "/",
                async Task<Created<Guid>> (
                    Guid storageId,
                    ItemRequest request,
                    AddItemUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    var item = await useCase.ExecuteAsync(
                        new AddItemInput(
                            storageId,
                            request.Name,
                            request.Amount,
                            request.Unit,
                            request.ExpiryDate,
                            request.ProductionDate
                        ),
                        ct
                    );
                    return TypedResults.Created(
                        $"/api/v1/storages/{storageId}/items/{item.Id}",
                        item.Id
                    );
                }
            )
            .WithName("addItem")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        items
            .MapPut(
                "/{itemId:guid}",
                async Task<NoContent> (
                    Guid storageId,
                    Guid itemId,
                    ItemRequest request,
                    UpdateItemUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    // AC-08: amount 0 removes the item — both outcomes are 204
                    await useCase.ExecuteAsync(
                        new UpdateItemInput(
                            storageId,
                            itemId,
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
                "/{itemId:guid}",
                async Task<NoContent> (
                    Guid storageId,
                    Guid itemId,
                    DeleteItemUseCase useCase,
                    CancellationToken ct
                ) =>
                {
                    await useCase.ExecuteAsync(storageId, itemId, ct);
                    return TypedResults.NoContent();
                }
            )
            .WithName("deleteItem")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
