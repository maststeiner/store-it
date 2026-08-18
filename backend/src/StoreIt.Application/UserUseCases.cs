using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>
/// Raised by <see cref="IUserRepository"/> when a concurrent first-login insert races to
/// a unique-key violation on (Issuer, Subject). Application stays free of EF/Npgsql types.
/// </summary>
public sealed class UserAlreadyExistsException()
    : Exception("A user with this issuer/subject already exists.");

/// <summary>
/// Find-or-create a user account keyed by (issuer, subject) and refresh mutable profile
/// fields on every login (SPEC-003 EC-01 / EC-02).
/// Race handling: on concurrent first login the losing thread catches
/// <see cref="UserAlreadyExistsException"/>, reloads the winner, and refreshes it.
/// </summary>
public sealed class ProvisionUserUseCase(IUserRepository repository, TimeProvider timeProvider)
{
    public async Task<User> ExecuteAsync(
        string issuer,
        string subject,
        string? email,
        string? displayName,
        CancellationToken cancellationToken
    )
    {
        var existing = await repository.GetBySubjectAsync(issuer, subject, cancellationToken);
        if (existing is not null)
        {
            existing.UpdateProfile(email, displayName);
            await repository.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var user = User.Create(issuer, subject, email, displayName, timeProvider.GetUtcNow());
        repository.Add(user);
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (UserAlreadyExistsException)
        {
            // A concurrent first-login won the race; reload the winner and refresh its profile.
            var winner =
                await repository.GetBySubjectAsync(issuer, subject, cancellationToken)
                ?? throw new InvalidOperationException(
                    "UserAlreadyExistsException was raised but the user cannot be found afterwards."
                );
            winner.UpdateProfile(email, displayName);
            await repository.SaveChangesAsync(cancellationToken);
            return winner;
        }

        return user;
    }
}
