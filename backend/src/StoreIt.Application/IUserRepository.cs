using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>
/// Port for the User aggregate (ADR-001: defined here, implemented in Infrastructure).
/// </summary>
public interface IUserRepository
{
    Task<User?> GetBySubjectAsync(string issuer, string subject, CancellationToken cancellationToken);

    void Add(User user);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
