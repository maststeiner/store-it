using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>What the owner sees about the storage's invitation link (AC-06) — never the token.</summary>
public sealed record InvitationStatus(bool Active, DateTimeOffset? ExpiresAt);

/// <summary>The freshly created link: the token is returned exactly once (AC-05).</summary>
public sealed record CreatedInvitation(string Token, DateTimeOffset ExpiresAt);

/// <summary>The join page (D3): what a recipient learns before joining (AC-08).</summary>
public sealed record InvitationPreview(
    Guid StorageId,
    string StorageName,
    string OwnerName,
    bool AlreadyMember
);

/// <summary>A row of the member list (AC-11): display name only (D6).</summary>
public sealed record StorageMemberInfo(
    Guid UserId,
    string DisplayName,
    bool IsOwner,
    DateTimeOffset? JoinedAt
);

internal static class SharingGuards
{
    internal static Guid RequireUser(this ICurrentUser currentUser) =>
        currentUser.UserId
        ?? throw new InvalidOperationException("This operation requires an authenticated user.");

    /// <summary>AC-04: owner-only operations (delete, invitation, members).</summary>
    internal static void EnsureOwner(this Storage storage, Guid userId)
    {
        if (!storage.IsOwner(userId))
        {
            throw new StorageOwnerOnlyException(storage.Id);
        }
    }

    internal static string NameOf(this IReadOnlyDictionary<Guid, string> names, Guid userId) =>
        names.TryGetValue(userId, out var name) ? name : "?";
}

/// <summary>AC-05: create (or replace) the storage's invitation link — owner only.</summary>
public sealed class CreateInvitationUseCase(
    IStorageRepository storages,
    IInvitationRepository invitations,
    IInvitationTokens tokens,
    ICurrentUser currentUser,
    TimeProvider timeProvider
)
{
    public async Task<CreatedInvitation> ExecuteAsync(
        Guid storageId,
        CancellationToken cancellationToken
    )
    {
        var storage = await storages.GetRequiredAsync(storageId, cancellationToken);
        storage.EnsureOwner(currentUser.RequireUser());

        var existing = await invitations.GetByStorageIdAsync(storageId, cancellationToken);
        if (existing is not null)
        {
            invitations.Remove(existing);
        }

        var token = tokens.NewToken();
        var invitation = StorageInvitation.Create(
            storageId,
            tokens.Hash(token),
            timeProvider.GetUtcNow()
        );
        invitations.Add(invitation);
        await invitations.SaveChangesAsync(cancellationToken);
        return new CreatedInvitation(token, invitation.ExpiresAt);
    }
}

/// <summary>AC-06: is there an active link, and until when — owner only.</summary>
public sealed class GetInvitationUseCase(
    IStorageRepository storages,
    IInvitationRepository invitations,
    ICurrentUser currentUser,
    TimeProvider timeProvider
)
{
    public async Task<InvitationStatus> ExecuteAsync(
        Guid storageId,
        CancellationToken cancellationToken
    )
    {
        var storage = await storages.GetRequiredAsync(storageId, cancellationToken);
        storage.EnsureOwner(currentUser.RequireUser());

        var invitation = await invitations.GetByStorageIdAsync(storageId, cancellationToken);
        return invitation is not null && invitation.IsActive(timeProvider.GetUtcNow())
            ? new InvitationStatus(true, invitation.ExpiresAt)
            : new InvitationStatus(false, null);
    }
}

/// <summary>AC-07: deactivate the link immediately — owner only. Idempotent.</summary>
public sealed class DeactivateInvitationUseCase(
    IStorageRepository storages,
    IInvitationRepository invitations,
    ICurrentUser currentUser
)
{
    public async Task ExecuteAsync(Guid storageId, CancellationToken cancellationToken)
    {
        var storage = await storages.GetRequiredAsync(storageId, cancellationToken);
        storage.EnsureOwner(currentUser.RequireUser());

        var invitation = await invitations.GetByStorageIdAsync(storageId, cancellationToken);
        if (invitation is null)
        {
            return;
        }

        invitations.Remove(invitation);
        await invitations.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// AC-08: what a recipient sees on the join page. Unknown, expired and deactivated tokens
/// all answer the same way (<see cref="InvitationInvalidException"/>) — a token cannot be
/// probed for its state.
/// </summary>
public sealed class PreviewInvitationUseCase(
    IStorageRepository storages,
    IInvitationRepository invitations,
    IUserRepository users,
    IInvitationTokens tokens,
    ICurrentUser currentUser,
    TimeProvider timeProvider
)
{
    public async Task<InvitationPreview> ExecuteAsync(
        string token,
        CancellationToken cancellationToken
    )
    {
        var userId = currentUser.RequireUser();
        var storage = await ResolveAsync(
            token,
            storages,
            invitations,
            tokens,
            timeProvider,
            cancellationToken
        );
        var names = await users.GetDisplayNamesAsync([storage.OwnerId], cancellationToken);
        return new InvitationPreview(
            storage.Id,
            storage.Name,
            names.NameOf(storage.OwnerId),
            storage.HasAccess(userId)
        );
    }

    /// <summary>Shared by preview and accept: token → active invitation → its storage.</summary>
    internal static async Task<Storage> ResolveAsync(
        string token,
        IStorageRepository storages,
        IInvitationRepository invitations,
        IInvitationTokens tokens,
        TimeProvider timeProvider,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvitationInvalidException();
        }

        var invitation = await invitations.GetByTokenHashAsync(
            tokens.Hash(token.Trim()),
            cancellationToken
        );
        if (invitation is null || !invitation.IsActive(timeProvider.GetUtcNow()))
        {
            throw new InvitationInvalidException();
        }

        // The caller is not a member yet, so the access filter must be bypassed here — and
        // only here; the invitation itself is the proof of legitimacy.
        return await storages.GetByIdIgnoringAccessAsync(invitation.StorageId, cancellationToken)
            ?? throw new InvitationInvalidException();
    }
}

/// <summary>AC-09/AC-10: join the storage behind a valid token. Idempotent for owner and members.</summary>
public sealed class AcceptInvitationUseCase(
    IStorageRepository storages,
    IInvitationRepository invitations,
    IInvitationTokens tokens,
    ICurrentUser currentUser,
    TimeProvider timeProvider
)
{
    public async Task<Guid> ExecuteAsync(string token, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUser();
        var storage = await PreviewInvitationUseCase.ResolveAsync(
            token,
            storages,
            invitations,
            tokens,
            timeProvider,
            cancellationToken
        );

        if (storage.AddMember(userId, timeProvider.GetUtcNow()))
        {
            try
            {
                await storages.SaveChangesAsync(cancellationToken);
            }
            catch (MemberAlreadyExistsException)
            {
                // A concurrent accept by the same user won the race — the outcome is the same.
            }
        }

        return storage.Id;
    }
}

/// <summary>AC-11: owner first, then members by join date — display names only (D6, D7).</summary>
public sealed class ListMembersUseCase(IStorageRepository storages, IUserRepository users)
{
    public async Task<IReadOnlyList<StorageMemberInfo>> ExecuteAsync(
        Guid storageId,
        CancellationToken cancellationToken
    )
    {
        var storage = await storages.GetRequiredAsync(storageId, cancellationToken);
        var ids = new List<Guid> { storage.OwnerId };
        ids.AddRange(storage.Members.Select(m => m.UserId));
        var names = await users.GetDisplayNamesAsync(ids, cancellationToken);

        var result = new List<StorageMemberInfo>
        {
            new(storage.OwnerId, names.NameOf(storage.OwnerId), true, null),
        };
        result.AddRange(
            storage
                .Members.OrderBy(m => m.JoinedAt)
                .Select(m => new StorageMemberInfo(
                    m.UserId,
                    names.NameOf(m.UserId),
                    false,
                    m.JoinedAt
                ))
        );
        return result;
    }
}

/// <summary>AC-12: the owner removes a member. Idempotent — removing a non-member changes nothing.</summary>
public sealed class RemoveMemberUseCase(IStorageRepository storages, ICurrentUser currentUser)
{
    public async Task ExecuteAsync(
        Guid storageId,
        Guid memberUserId,
        CancellationToken cancellationToken
    )
    {
        var storage = await storages.GetRequiredAsync(storageId, cancellationToken);
        storage.EnsureOwner(currentUser.RequireUser());

        if (storage.RemoveMember(memberUserId))
        {
            await storages.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>AC-13/AC-14: a member leaves; the owner cannot (409).</summary>
public sealed class LeaveStorageUseCase(IStorageRepository storages, ICurrentUser currentUser)
{
    public async Task ExecuteAsync(Guid storageId, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUser();
        var storage = await storages.GetRequiredAsync(storageId, cancellationToken);
        if (storage.IsOwner(userId))
        {
            throw new OwnerCannotLeaveException(storageId);
        }

        if (storage.RemoveMember(userId))
        {
            await storages.SaveChangesAsync(cancellationToken);
        }
    }
}
