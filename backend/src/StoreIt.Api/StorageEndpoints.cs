using StoreIt.Application;

namespace StoreIt.Api;

public static class StorageEndpoints
{
    /// <summary>SPEC-001 endpoints under /api/v1 (ADR-006 URL versioning).</summary>
    public static IEndpointRouteBuilder MapStorageEndpointsV1(this IEndpointRouteBuilder app)
    {
        var storages = app.MapGroup("/api/v1/storages").WithTags("Storages");

        storages.MapGet(
            "/",
            async (ListStoragesUseCase useCase, CancellationToken ct) =>
                Results.Ok((await useCase.ExecuteAsync(ct)).Select(StorageResponse.From))
        );

        storages.MapPost(
            "/",
            async (StorageRequest request, CreateStorageUseCase useCase, CancellationToken ct) =>
            {
                var storage = await useCase.ExecuteAsync(request.Name, ct);
                return Results.Created(
                    $"/api/v1/storages/{storage.Id}",
                    StorageResponse.From(storage)
                );
            }
        );

        storages.MapPut(
            "/{storageId:guid}",
            async (
                Guid storageId,
                StorageRequest request,
                RenameStorageUseCase useCase,
                CancellationToken ct
            ) =>
                Results.Ok(
                    StorageResponse.From(await useCase.ExecuteAsync(storageId, request.Name, ct))
                )
        );

        storages.MapDelete(
            "/{storageId:guid}",
            async (Guid storageId, DeleteStorageUseCase useCase, CancellationToken ct) =>
            {
                await useCase.ExecuteAsync(storageId, ct);
                return Results.NoContent();
            }
        );

        var items = storages.MapGroup("/{storageId:guid}/items").WithTags("Items");

        items.MapGet(
            "/",
            async (Guid storageId, GetStorageItemsUseCase useCase, CancellationToken ct) =>
                Results.Ok((await useCase.ExecuteAsync(storageId, ct)).Select(ItemResponse.From))
        );

        items.MapPost(
            "/",
            async (
                Guid storageId,
                ItemRequest request,
                AddItemUseCase useCase,
                CancellationToken ct
            ) =>
            {
                var item = await useCase.ExecuteAsync(
                    storageId,
                    request.Name,
                    request.Amount,
                    request.Unit,
                    request.ExpiryDate,
                    request.ProductionDate,
                    ct
                );
                return Results.Created($"/api/v1/storages/{storageId}/items/{item.Id}", item.Id);
            }
        );

        items.MapPut(
            "/{itemId:guid}",
            async (
                Guid storageId,
                Guid itemId,
                ItemRequest request,
                UpdateItemUseCase useCase,
                CancellationToken ct
            ) =>
            {
                // AC-08: amount 0 removes the item — both outcomes are 204
                await useCase.ExecuteAsync(
                    storageId,
                    itemId,
                    request.Name,
                    request.Amount,
                    request.Unit,
                    request.ExpiryDate,
                    request.ProductionDate,
                    ct
                );
                return Results.NoContent();
            }
        );

        items.MapDelete(
            "/{itemId:guid}",
            async (Guid storageId, Guid itemId, DeleteItemUseCase useCase, CancellationToken ct) =>
            {
                await useCase.ExecuteAsync(storageId, itemId, ct);
                return Results.NoContent();
            }
        );

        return app;
    }
}
