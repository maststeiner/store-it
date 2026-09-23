using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>
/// Application projection of a storage with server-computed status counts (spec
/// addendum, PO decision 2026-07-19 — status logic stays server-side, ADR-002).
/// Scalar fields only: the domain entity does not cross out of the Application layer.
/// </summary>
public sealed record StorageSummary(
    Guid Id,
    string Name,
    int ItemCount,
    int ExpiredCount,
    int ExpiringSoonCount,
    bool IsOwner,
    int MemberCount,
    string OwnerName
)
{
    /// <summary>
    /// SPEC-007 AC-01: <paramref name="viewerId"/> decides <c>IsOwner</c>; <c>MemberCount</c>
    /// counts members without the owner; <c>OwnerName</c> is the owner's display name (D6).
    /// </summary>
    public static StorageSummary From(
        Storage storage,
        DateOnly today,
        Guid? viewerId,
        string ownerName
    )
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

        return new StorageSummary(
            storage.Id,
            storage.Name,
            storage.Items.Count,
            expired,
            expiringSoon,
            viewerId is not null && storage.IsOwner(viewerId.Value),
            storage.Members.Count,
            ownerName
        );
    }
}

/// <summary>Resolves owner display names for summaries (one query per request, SPEC-007 D6).</summary>
public sealed class StorageSummaries(
    IUserRepository users,
    ICurrentUser currentUser,
    TimeProvider timeProvider
)
{
    public async Task<StorageSummary> OneAsync(Storage storage, CancellationToken cancellationToken)
    {
        var names = await users.GetDisplayNamesAsync([storage.OwnerId], cancellationToken);
        return StorageSummary.From(
            storage,
            timeProvider.Today(),
            currentUser.UserId,
            names.NameOf(storage.OwnerId)
        );
    }

    public async Task<IReadOnlyList<StorageSummary>> ManyAsync(
        IReadOnlyList<Storage> storages,
        CancellationToken cancellationToken
    )
    {
        var names = await users.GetDisplayNamesAsync(
            storages.Select(s => s.OwnerId).Distinct().ToList(),
            cancellationToken
        );
        var today = timeProvider.Today();
        return storages
            .Select(s => StorageSummary.From(s, today, currentUser.UserId, names.NameOf(s.OwnerId)))
            .ToList();
    }
}

/// <summary>AC-01: create a storage and return it in the storage list.</summary>
public sealed class CreateStorageUseCase(
    IStorageRepository repository,
    ICurrentUser currentUser,
    StorageSummaries summaries
)
{
    public async Task<StorageSummary> ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        // SPEC-003: the owner is stamped server-side from the authenticated session.
        // Endpoints require authentication via the fallback policy, so UserId is present
        // here. Guard defensively rather than persist an ownerless storage.
        // NOTE: the wording avoids a parenthesis plus trailing semicolon — that shape made
        // Sonar S125 misread this prose as commented-out code.
        var ownerId =
            currentUser.UserId
            ?? throw new InvalidOperationException(
                "Cannot create a storage without an authenticated user."
            );

        var storage = Storage.Create(name, ownerId);
        repository.Add(storage);
        await repository.SaveChangesAsync(cancellationToken);
        return await summaries.OneAsync(storage, cancellationToken);
    }
}

/// <summary>AC-01: list all storages with status counts — owned and shared (SPEC-007 AC-01).</summary>
public sealed class ListStoragesUseCase(IStorageRepository repository, StorageSummaries summaries)
{
    public async Task<IReadOnlyList<StorageSummary>> ExecuteAsync(
        CancellationToken cancellationToken
    ) =>
        await summaries.ManyAsync(
            await repository.GetAllAsync(cancellationToken),
            cancellationToken
        );
}

/// <summary>Get a single storage with status counts — lets a client refresh one
/// storage without fetching the whole list (#29).</summary>
public sealed class GetStorageUseCase(IStorageRepository repository, StorageSummaries summaries)
{
    public async Task<StorageSummary> ExecuteAsync(
        Guid storageId,
        CancellationToken cancellationToken
    )
    {
        var storage = await repository.GetRequiredAsync(storageId, cancellationToken);
        return await summaries.OneAsync(storage, cancellationToken);
    }
}

/// <summary>AC-03: rename a storage (SPEC-007 D1: members may, too).</summary>
public sealed class RenameStorageUseCase(IStorageRepository repository, StorageSummaries summaries)
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
        return await summaries.OneAsync(storage, cancellationToken);
    }
}

/// <summary>
/// AC-04: delete a storage including all of its items (EC-06: no orphans). SPEC-007 AC-04:
/// owner only — members leave instead (<see cref="LeaveStorageUseCase"/>).
/// </summary>
public sealed class DeleteStorageUseCase(IStorageRepository repository, ICurrentUser currentUser)
{
    public async Task ExecuteAsync(Guid storageId, CancellationToken cancellationToken)
    {
        var storage = await repository.GetRequiredAsync(storageId, cancellationToken);
        storage.EnsureOwner(currentUser.RequireUser());
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
