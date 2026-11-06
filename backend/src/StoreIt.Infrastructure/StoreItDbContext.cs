using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
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
///
/// Also the store of the ASP.NET Core data-protection key ring
/// (<see cref="IDataProtectionKeyContext"/>): the keys that encrypt the session and CSRF
/// cookies must outlive the container, or every release signs everyone out.
/// </summary>
public class StoreItDbContext(DbContextOptions<StoreItDbContext> options, ICurrentUser currentUser)
    : DbContext(options),
        IDataProtectionKeyContext
{
    public DbSet<Storage> Storages => Set<Storage>();
    public DbSet<User> Users => Set<User>();
    public DbSet<StorageInvitation> StorageInvitations => Set<StorageInvitation>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoreItDbContext).Assembly);

        // Framework-owned table (key id, friendly name, key XML); snake_case like the rest.
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        // Access isolation (SPEC-003 ownership, widened by SPEC-007 sharing / ADR-008 to
        // "owner or member"): anonymous ⇒ UserId is null ⇒ matches nothing.
        modelBuilder
            .Entity<Storage>()
            .HasQueryFilter(s =>
                s.OwnerId == currentUser.UserId
                || s.Members.Any(m => m.UserId == currentUser.UserId)
            );
    }
}
