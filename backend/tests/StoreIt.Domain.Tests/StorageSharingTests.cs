using StoreIt.Domain;

namespace StoreIt.Domain.Tests;

/// <summary>SPEC-007 / ADR-008: membership and invitation rules on the aggregate.</summary>
public class StorageSharingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddMember_NewUser_AddsAndGrantsAccess()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var storage = Storage.Create("Pantry", owner);

        var added = storage.AddMember(member, Now);

        Assert.True(added);
        Assert.True(storage.HasAccess(member));
        Assert.False(storage.IsOwner(member));
        Assert.Single(storage.Members);
    }

    [Fact]
    public void AddMember_OwnerOrExistingMember_IsIdempotent()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var storage = Storage.Create("Pantry", owner);
        storage.AddMember(member, Now);

        Assert.False(storage.AddMember(owner, Now));
        Assert.False(storage.AddMember(member, Now.AddMinutes(1)));
        Assert.Single(storage.Members);
    }

    [Fact]
    public void AddMember_EmptyUser_ThrowsValidation()
    {
        var storage = Storage.Create("Pantry", Guid.NewGuid());

        var ex = Assert.Throws<DomainValidationException>(() => storage.AddMember(Guid.Empty, Now));
        Assert.Equal("storage.member.missing", ex.ErrorCode);
    }

    [Fact]
    public void RemoveMember_ExistingAndUnknown_ReportsWhatHappened()
    {
        var member = Guid.NewGuid();
        var storage = Storage.Create("Pantry", Guid.NewGuid());
        storage.AddMember(member, Now);

        Assert.True(storage.RemoveMember(member));
        Assert.False(storage.RemoveMember(member));
        Assert.False(storage.HasAccess(member));
    }

    [Fact]
    public void IsActive_WithinSevenDays_ReturnsTrueUntilExpiry()
    {
        var invitation = StorageInvitation.Create(Guid.NewGuid(), "hash", Now);

        Assert.Equal(Now.AddDays(7), invitation.ExpiresAt);
        Assert.True(invitation.IsActive(Now));
        Assert.True(invitation.IsActive(Now.AddDays(7).AddSeconds(-1)));
        Assert.False(invitation.IsActive(Now.AddDays(7)));
    }

    [Fact]
    public void Create_EmptyHash_ThrowsValidation()
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            StorageInvitation.Create(Guid.NewGuid(), " ", Now)
        );
        Assert.Equal("invitation.tokenHash.empty", ex.ErrorCode);
    }

    [Fact]
    public void TransferOwnership_ToMember_SwapsRoles()
    {
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var storage = Storage.Create("Pantry", owner);
        storage.AddMember(member, Now);

        var transferred = storage.TransferOwnership(member, Now.AddDays(1));

        Assert.True(transferred);
        Assert.True(storage.IsOwner(member));
        Assert.False(storage.IsOwner(owner));
        Assert.True(storage.IsMember(owner));
        Assert.False(storage.IsMember(member));
        Assert.Single(storage.Members);
    }

    [Fact]
    public void TransferOwnership_ToNonMemberOrSelf_ChangesNothing()
    {
        var owner = Guid.NewGuid();
        var storage = Storage.Create("Pantry", owner);

        Assert.False(storage.TransferOwnership(Guid.NewGuid(), Now));
        Assert.True(storage.TransferOwnership(owner, Now));
        Assert.True(storage.IsOwner(owner));
        Assert.Empty(storage.Members);
    }
}
