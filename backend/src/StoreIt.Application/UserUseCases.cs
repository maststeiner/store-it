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

/// <summary>
/// SPEC-006 D6 / AC-05: a write arrived with a session whose user row no longer exists
/// (the account was deleted while another device kept its cookie). Raised by the
/// Infrastructure layer when the owner foreign key is violated; the API maps it to 401
/// and ends the stale session. Application stays free of EF/Npgsql types.
/// </summary>
public sealed class OwnerNoLongerExistsException()
    : Exception("The signed-in account no longer exists.");

/// <summary>
/// SPEC-006 AC-01: delete the current user's account. The database cascades the deletion
/// to the storages and items the user owns (SPEC-003 schema), so one statement removes
/// everything in one transaction. Idempotent (EC-01): an account that is already gone —
/// two tabs confirming at once — counts as deleted.
/// </summary>
public sealed class DeleteAccountUseCase(IUserRepository repository, ICurrentUser currentUser)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var userId =
            currentUser.UserId
            ?? throw new InvalidOperationException(
                "DeleteAccountUseCase requires an authenticated user."
            );

        var user = await repository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        repository.Remove(user);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
