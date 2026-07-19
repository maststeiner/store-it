using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>
/// Port for the Storage aggregate (ADR-001: defined here, implemented in Infrastructure).
/// </summary>
public interface IStorageRepository
{
    Task<Storage?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Storage>> GetAllAsync(CancellationToken cancellationToken);

    void Add(Storage storage);

    void Remove(Storage storage);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
