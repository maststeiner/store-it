using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>An item together with its server-computed expiry status (AC-11/AC-12).</summary>
public sealed record ItemWithStatus(Item Item, ExpiryStatus Status);

/// <summary>
/// AC-10: items of a storage, sorted by expiry date ascending (items without expiry
/// date last), each with its expiry status. Status computation is server-side domain
/// logic (ADR-002: no business rules in clients).
/// </summary>
public sealed class GetStorageItemsUseCase(IStorageRepository repository, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ItemWithStatus>> ExecuteAsync(
        Guid storageId,
        CancellationToken cancellationToken
    )
    {
        var storage = await repository.GetRequiredAsync(storageId, cancellationToken);
        var today = timeProvider.Today();

        return storage
            .GetItemsSortedByExpiry()
            .Select(item => new ItemWithStatus(item, item.GetExpiryStatus(today)))
            .ToList();
    }
}

/// <summary>Input for <see cref="AddItemUseCase"/> (AC-05/AC-06).</summary>
public sealed record AddItemInput(
    Guid StorageId,
    string Name,
    decimal Amount,
    Unit Unit,
    DateOnly? ExpiryDate,
    DateOnly? ProductionDate
);

/// <summary>AC-05/AC-06: add an item to a storage.</summary>
public sealed class AddItemUseCase(IStorageRepository repository)
{
    public async Task<Item> ExecuteAsync(AddItemInput command, CancellationToken cancellationToken)
    {
        var storage = await repository.GetRequiredAsync(command.StorageId, cancellationToken);
        var item = storage.AddItem(
            command.Name,
            command.Amount,
            command.Unit,
            command.ExpiryDate,
            command.ProductionDate
        );
        await repository.SaveChangesAsync(cancellationToken);
        return item;
    }
}

/// <summary>Input for <see cref="UpdateItemUseCase"/> (AC-07/AC-08).</summary>
public sealed record UpdateItemInput(
    Guid StorageId,
    Guid ItemId,
    string Name,
    decimal Amount,
    Unit Unit,
    DateOnly? ExpiryDate,
    DateOnly? ProductionDate
);

/// <summary>
/// AC-07/AC-08: update an item; amount 0 removes it. Returns false when removed.
/// </summary>
public sealed class UpdateItemUseCase(IStorageRepository repository)
{
    public async Task<bool> ExecuteAsync(
        UpdateItemInput command,
        CancellationToken cancellationToken
    )
    {
        var storage = await repository.GetRequiredAsync(command.StorageId, cancellationToken);
        var kept = storage.UpdateItem(
            command.ItemId,
            command.Name,
            command.Amount,
            command.Unit,
            command.ExpiryDate,
            command.ProductionDate
        );
        await repository.SaveChangesAsync(cancellationToken);
        return kept;
    }
}

/// <summary>AC-09: delete an item regardless of amount.</summary>
public sealed class DeleteItemUseCase(IStorageRepository repository)
{
    public async Task ExecuteAsync(Guid storageId, Guid itemId, CancellationToken cancellationToken)
    {
        var storage = await repository.GetRequiredAsync(storageId, cancellationToken);
        storage.RemoveItem(itemId);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
