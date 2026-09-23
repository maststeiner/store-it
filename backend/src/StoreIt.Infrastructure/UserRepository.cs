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

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);

    // SPEC-006: the FK from storages (and from items to storages) is ON DELETE CASCADE, so
    // PostgreSQL removes the user's data in the same statement — no need to load it here.
    public void Remove(User user) => dbContext.Users.Remove(user);

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
