using Microsoft.EntityFrameworkCore;
using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Infrastructure;

public sealed class UserRepository(StoreItDbContext dbContext) : IUserRepository
{
    public Task<User?> GetBySubjectAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken
    ) =>
        dbContext.Users.FirstOrDefaultAsync(
            u => u.Issuer == issuer && u.Subject == subject,
            cancellationToken
        );

    public void Add(User user) => dbContext.Users.Add(user);

    // SPEC-006: a set-based delete — no load, no tracked entity, so a concurrent second delete
    // affects 0 rows and simply succeeds instead of raising DbUpdateConcurrencyException. The
    // FK from storages (and from items to storages) is ON DELETE CASCADE, so PostgreSQL removes
    // the user's data in the same statement.
    public Task DeleteByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.Where(u => u.Id == id).ExecuteDeleteAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Detach the failed insert so a retry (reload + refresh) does not re-add it.
            foreach (
                var e in dbContext
                    .ChangeTracker.Entries<User>()
                    .Where(e => e.State == EntityState.Added)
            )
                e.State = EntityState.Detached;
            throw new UserAlreadyExistsException();
        }
    }
}
