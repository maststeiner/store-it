using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>Port for storage invitations (SPEC-007 D2), implemented in Infrastructure (ADR-001).</summary>
public interface IInvitationRepository
{
    Task<StorageInvitation?> GetByStorageIdAsync(
        Guid storageId,
        CancellationToken cancellationToken
    );

    Task<StorageInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken
    );

    void Add(StorageInvitation invitation);

    void Remove(StorageInvitation invitation);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Port for the invitation token itself (ADR-008 decision 3): random, opaque, stored hashed.
/// Generation and hashing are infrastructure concerns; the domain only ever sees the hash.
/// </summary>
public interface IInvitationTokens
{
    /// <summary>A new URL-safe token with at least 128 bit of entropy.</summary>
    string NewToken();

    /// <summary>The stable hash under which a token is stored and looked up.</summary>
    string Hash(string token);
}
