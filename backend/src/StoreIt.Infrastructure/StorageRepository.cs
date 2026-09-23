using Microsoft.EntityFrameworkCore;
using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Infrastructure;

public sealed class StorageRepository(StoreItDbContext dbContext) : IStorageRepository
{
    public Task<Storage?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext
            .Storages.Include(s => s.Items)
            .Include(s => s.Members)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    // SPEC-007 AC-09: only for redeeming an invitation — the caller is not a member yet.
    public Task<Storage?> GetByIdIgnoringAccessAsync(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        dbContext
            .Storages.IgnoreQueryFilters()
            .Include(s => s.Items)
            .Include(s => s.Members)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Storage>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext
            .Storages.Include(s => s.Items)
            .Include(s => s.Members)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public void Add(Storage storage) => dbContext.Storages.Add(storage);

    public void Remove(Storage storage) => dbContext.Storages.Remove(storage);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is Npgsql.PostgresException
                    {
                        SqlState: "23503",
                        ConstraintName: OwnerForeignKey
                    }
            )
        {
            // SPEC-006 D6: the session's user was deleted meanwhile (another device kept
            // its cookie). Only the owner FK maps to "stale session"; an item whose storage
            // vanished still surfaces through the aggregate load as a 404.
            throw new OwnerNoLongerExistsException();
        }
    }

    /// <summary>Constraint name EF Core generated for <c>storages.OwnerId → users.Id</c>.</summary>
    private const string OwnerForeignKey = "FK_storages_users_OwnerId";
}
