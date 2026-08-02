using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>
/// A storage with server-computed status counts for the overview chips
/// (spec addendum, PO decision 2026-07-19 — status logic stays server-side, ADR-002).
/// </summary>
public sealed record StorageSummary(Storage Storage, int ExpiredCount, int ExpiringSoonCount)
{
    public static StorageSummary From(Storage storage, DateOnly today)
    {
        var expired = 0;
        var expiringSoon = 0;
        foreach (var item in storage.Items)
        {
            switch (item.GetExpiryStatus(today))
            {
                case ExpiryStatus.Expired:
                    expired++;
                    break;
                case ExpiryStatus.ExpiringSoon:
                    expiringSoon++;
                    break;
                case ExpiryStatus.Ok:
                    break;
            }
        }

        return new StorageSummary(storage, expired, expiringSoon);
    }
}

/// <summary>AC-01: create a storage and return it in the storage list.</summary>
public sealed class CreateStorageUseCase(IStorageRepository repository, TimeProvider timeProvider)
{
    public async Task<StorageSummary> ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        var storage = Storage.Create(name);
        repository.Add(storage);
        await repository.SaveChangesAsync(cancellationToken);
        return StorageSummary.From(storage, timeProvider.Today());
    }
}

/// <summary>AC-01: list all storages with status counts.</summary>
public sealed class ListStoragesUseCase(IStorageRepository repository, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<StorageSummary>> ExecuteAsync(
        CancellationToken cancellationToken
    )
    {
        var today = timeProvider.Today();
        return (await repository.GetAllAsync(cancellationToken))
            .Select(storage => StorageSummary.From(storage, today))
            .ToList();
    }
}

/// <summary>Get a single storage with status counts — lets a client refresh one
/// storage without fetching the whole list (#29).</summary>
public sealed class GetStorageUseCase(IStorageRepository repository, TimeProvider timeProvider)
{
    public async Task<StorageSummary> ExecuteAsync(
        Guid storageId,
        CancellationToken cancellationToken
    )
    {
        var storage = await repository.GetRequiredAsync(storageId, cancellationToken);
        return StorageSummary.From(storage, timeProvider.Today());
    }
}

/// <summary>AC-03: rename a storage.</summary>
public sealed class RenameStorageUseCase(IStorageRepository repository, TimeProvider timeProvider)
{
    public async Task<StorageSummary> ExecuteAsync(
        Guid storageId,
        string name,
        CancellationToken cancellationToken
    )
    {
        var storage = await repository.GetRequiredAsync(storageId, cancellationToken);
        storage.Rename(name);
        await repository.SaveChangesAsync(cancellationToken);
        return StorageSummary.From(storage, timeProvider.Today());
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

internal static class TimeProviderExtensions
{
    internal static DateOnly Today(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);
}
