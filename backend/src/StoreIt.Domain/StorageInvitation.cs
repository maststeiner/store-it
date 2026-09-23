namespace StoreIt.Domain;

/// <summary>
/// SPEC-007 D2 / ADR-008 decision 3: the one active invitation link of a storage. Holds only
/// the SHA-256 hash of the token — the plain token exists once, in the owner's browser.
/// Token generation and hashing happen outside the domain (Application port).
/// </summary>
public class StorageInvitation
{
    /// <summary>D2: a link is valid for seven days.</summary>
    public static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    public Guid StorageId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private StorageInvitation() { } // EF Core

    private StorageInvitation(Guid storageId, string tokenHash, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainValidationException(
                "invitation.tokenHash.empty",
                "Invitation token hash must not be empty."
            );
        }

        StorageId = storageId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = createdAt + Validity;
    }

    public static StorageInvitation Create(
        Guid storageId,
        string tokenHash,
        DateTimeOffset createdAt
    ) => new(storageId, tokenHash, createdAt);

    /// <summary>AC-08/AC-10: usable only until it expires; deactivation deletes the row.</summary>
    public bool IsActive(DateTimeOffset now) => now < ExpiresAt;
}
