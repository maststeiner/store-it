namespace StoreIt.Domain;

/// <summary>
/// SPEC-007 / ADR-008: a non-owner user with full access to a storage. Lives and dies with
/// the storage (aggregate child); the owner is <see cref="Storage.OwnerId"/>, never a member.
/// </summary>
public class StorageMember
{
    public Guid StorageId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    private StorageMember() { } // EF Core

    internal StorageMember(Guid storageId, Guid userId, DateTimeOffset joinedAt)
    {
        StorageId = storageId;
        UserId = userId;
        JoinedAt = joinedAt;
    }
}
