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
