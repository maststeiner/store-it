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

    void Add(User user);

    /// <summary>
    /// SPEC-006: delete the account if it exists — one statement, storages and items go with it
    /// (database cascade). Deleting a row that is already gone is a successful no-op (EC-01),
    /// so two tabs confirming at once never turn into a concurrency error.
    /// </summary>
    Task DeleteByIdAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
