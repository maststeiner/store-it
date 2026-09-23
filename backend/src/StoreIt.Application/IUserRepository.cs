using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>
/// Port for the User aggregate (ADR-001: defined here, implemented in Infrastructure).
/// </summary>
public interface IUserRepository
{
    Task<User?> GetBySubjectAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken
    );

    /// <summary>The user with the internal id, or <c>null</c> (SPEC-006: deleted accounts).</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(User user);

    /// <summary>SPEC-006: remove the account; storages and items go with it (database cascade).</summary>
    void Remove(User user);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
