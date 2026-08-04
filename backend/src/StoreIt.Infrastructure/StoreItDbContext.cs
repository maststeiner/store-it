using Microsoft.EntityFrameworkCore;
using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Infrastructure;

/// <summary>
/// SPEC-003 ownership isolation: a global query filter scopes every Storage read to
/// the current user. Items are reachable only through the Storage aggregate
/// (<c>Include(s =&gt; s.Items)</c>, no <c>DbSet&lt;Item&gt;</c>), so the storage-level
/// filter fully covers items (AC-11). An anonymous request (<c>UserId == null</c>)
/// matches no storage — a by-id lookup of another user's storage returns <c>null</c>,
/// surfacing as the existing <c>StorageNotFoundException</c> → 404 (AC-10).
/// </summary>
public class StoreItDbContext(DbContextOptions<StoreItDbContext> options, ICurrentUser currentUser)
    : DbContext(options)
{
    public DbSet<Storage> Storages => Set<Storage>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoreItDbContext).Assembly);

        // Ownership isolation (SPEC-003): anonymous ⇒ UserId is null ⇒ matches nothing.
        modelBuilder.Entity<Storage>().HasQueryFilter(s => s.OwnerId == currentUser.UserId);
    }
}
