using Microsoft.EntityFrameworkCore;
using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Infrastructure;

public sealed class StorageRepository(StoreItDbContext dbContext) : IStorageRepository
{
    public Task<Storage?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext
            .Storages.Include(s => s.Items)
                .ThenInclude(i => i.Tags)
            .Include(s => s.Tags)
            .Include(s => s.Members)
            // Several collection includes → one query would multiply rows (Sonar S8733).
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    // SPEC-007 AC-09: only for redeeming an invitation — the caller is not a member yet.
    // Preview and accept need id, name, owner and members; the items stay out.
    public Task<Storage?> GetByIdIgnoringAccessAsync(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        dbContext
            .Storages.IgnoreQueryFilters()
            .Include(s => s.Members)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Storage>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext
            .Storages.Include(s => s.Items)
                .ThenInclude(i => i.Tags)
            .Include(s => s.Tags)
            .Include(s => s.Members)
            // Several collection includes → one query would multiply rows (Sonar S8733).
            .AsSplitQuery()
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
            when (ex.InnerException is Npgsql.PostgresException { SqlState: "23503" } fk
                && fk.ConstraintName is OwnerForeignKey or MemberUserForeignKey
            )
        {
            // SPEC-006 D6 / SPEC-007 EC-08: the session's user was deleted meanwhile (another
            // device kept its cookie) — as storage owner or as a joining member. Only these two
            // constraints map to "stale session"; an item whose storage vanished still surfaces
            // through the aggregate load as a 404.
            throw new OwnerNoLongerExistsException();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is Npgsql.PostgresException
                    {
                        SqlState: "23505" or "23503",
                        ConstraintName: TagUniqueIndex or ItemTagTagForeignKey
                    }
            )
        {
            // SPEC-011: two members raced on the same storage's tags — same new tag twice
            // (unique index) or assign vs. prune (FK). A retry resolves it; never a 500.
            DetachAdded<Tag>();
            throw new TagConflictException();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is Npgsql.PostgresException
                    {
                        SqlState: "23505",
                        ConstraintName: MemberPrimaryKey
                    }
            )
        {
            // SPEC-007 AC-09: two accepts of the same user raced (double-click, two tabs). The
            // second insert loses; "already a member" is a success for the caller.
            DetachAdded<StorageMember>();
            throw new MemberAlreadyExistsException();
        }
    }

    /// <summary>Detach failed inserts so the context stays usable after a handled violation.</summary>
    private void DetachAdded<TEntity>()
        where TEntity : class
    {
        foreach (
            var entry in dbContext
                .ChangeTracker.Entries<TEntity>()
                .Where(e => e.State == EntityState.Added)
        )
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>Constraint name EF Core generated for <c>storage_members.UserId → users.Id</c>.</summary>
    private const string MemberUserForeignKey = "FK_storage_members_users_UserId";

    /// <summary>Primary key of <c>storage_members</c> (StorageId, UserId).</summary>
    private const string MemberPrimaryKey = "PK_storage_members";

    /// <summary>Constraint name EF Core generated for <c>storages.OwnerId → users.Id</c>.</summary>
    private const string OwnerForeignKey = "FK_storages_users_OwnerId";

    /// <summary>SPEC-011: unique index on <c>tags (storage_id, NormalizedName)</c>.</summary>
    private const string TagUniqueIndex = "IX_tags_storage_id_NormalizedName";

    /// <summary>SPEC-011: <c>item_tags.tag_id → tags.Id</c> — violated when the tag was pruned meanwhile.</summary>
    private const string ItemTagTagForeignKey = "FK_item_tags_tags_tag_id";
}
