using Microsoft.EntityFrameworkCore;
using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Infrastructure;

public sealed class StorageRepository(StoreItDbContext dbContext) : IStorageRepository
{
    public Task<Storage?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext
            .Storages.Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Storage>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext
            .Storages.Include(s => s.Items)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public void Add(Storage storage) => dbContext.Storages.Add(storage);

    public void Remove(Storage storage) => dbContext.Storages.Remove(storage);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
