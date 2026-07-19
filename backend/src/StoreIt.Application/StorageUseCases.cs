using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>AC-01: create a storage and return it in the storage list.</summary>
public sealed class CreateStorageUseCase(IStorageRepository repository)
{
    public async Task<Storage> ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        var storage = Storage.Create(name);
        repository.Add(storage);
        await repository.SaveChangesAsync(cancellationToken);
        return storage;
    }
}

/// <summary>AC-01: list all storages.</summary>
public sealed class ListStoragesUseCase(IStorageRepository repository)
{
    public Task<IReadOnlyList<Storage>> ExecuteAsync(CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);
}

/// <summary>AC-03: rename a storage.</summary>
public sealed class RenameStorageUseCase(IStorageRepository repository)
{
    public async Task<Storage> ExecuteAsync(
        Guid storageId,
        string name,
        CancellationToken cancellationToken
    )
    {
        var storage = await repository.GetRequiredAsync(storageId, cancellationToken);
        storage.Rename(name);
        await repository.SaveChangesAsync(cancellationToken);
        return storage;
    }
}

/// <summary>AC-04: delete a storage including all of its items (EC-06: no orphans).</summary>
public sealed class DeleteStorageUseCase(IStorageRepository repository)
{
    public async Task ExecuteAsync(Guid storageId, CancellationToken cancellationToken)
    {
        var storage = await repository.GetRequiredAsync(storageId, cancellationToken);
        repository.Remove(storage);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

internal static class StorageRepositoryExtensions
{
    internal static async Task<Storage> GetRequiredAsync(
        this IStorageRepository repository,
        Guid storageId,
        CancellationToken cancellationToken
    ) =>
        await repository.GetByIdAsync(storageId, cancellationToken)
        ?? throw new StorageNotFoundException(storageId);
}
