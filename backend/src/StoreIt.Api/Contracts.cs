using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Api;

// Boundary DTOs (ADR-001: domain entities do not leak through the API).
// Dates are ISO-8601 (DateOnly), enums are locale-neutral codes (arc42 §8 i18n).

public sealed record StorageResponse(
    Guid Id,
    string Name,
    int ItemCount,
    int ExpiredCount,
    int ExpiringSoonCount,
    bool IsOwner,
    int MemberCount,
    string OwnerName
)
{
    public static StorageResponse From(StorageSummary summary) =>
        new(
            summary.Id,
            summary.Name,
            summary.ItemCount,
            summary.ExpiredCount,
            summary.ExpiringSoonCount,
            summary.IsOwner,
            summary.MemberCount,
            summary.OwnerName
        );
}

// SPEC-007 sharing

public sealed record InvitationStatusResponse(bool Active, DateTimeOffset? ExpiresAt)
{
    public static InvitationStatusResponse From(InvitationStatus status) =>
        new(status.Active, status.ExpiresAt);
}

/// <summary>The token is returned exactly once, here (AC-05).</summary>
public sealed record CreatedInvitationResponse(string Token, DateTimeOffset ExpiresAt)
{
    public static CreatedInvitationResponse From(CreatedInvitation created) =>
        new(created.Token, created.ExpiresAt);
}

/// <summary>The token travels in the body, never in a URL (SPEC-007 D3 / EC-10).</summary>
public sealed record InvitationTokenRequest(string Token);

public sealed record InvitationPreviewResponse(
    Guid StorageId,
    string StorageName,
    string OwnerName,
    bool AlreadyMember
)
{
    public static InvitationPreviewResponse From(InvitationPreview preview) =>
        new(preview.StorageId, preview.StorageName, preview.OwnerName, preview.AlreadyMember);
}

public sealed record AcceptedInvitationResponse(Guid StorageId);

public sealed record StorageMemberResponse(
    Guid UserId,
    string DisplayName,
    bool IsOwner,
    DateTimeOffset? JoinedAt
)
{
    public static StorageMemberResponse From(StorageMemberInfo member) =>
        new(member.UserId, member.DisplayName, member.IsOwner, member.JoinedAt);
}

public sealed record ItemResponse(
    Guid Id,
    string Name,
    decimal Amount,
    Unit Unit,
    DateOnly? ExpiryDate,
    DateOnly? ProductionDate,
    ExpiryStatus ExpiryStatus
)
{
    public static ItemResponse From(ItemWithStatus itemWithStatus) =>
        new(
            itemWithStatus.Id,
            itemWithStatus.Name,
            itemWithStatus.Amount,
            itemWithStatus.Unit,
            itemWithStatus.ExpiryDate,
            itemWithStatus.ProductionDate,
            itemWithStatus.Status
        );
}

public sealed record StorageRequest(string Name);

public sealed record ItemRequest(
    string Name,
    decimal Amount,
    Unit Unit,
    DateOnly? ExpiryDate,
    DateOnly? ProductionDate
);

/// <summary>SPEC-007 AC-18: body of <c>PUT /api/v1/storages/{storageId}/owner</c>.</summary>
public sealed record TransferOwnershipRequest(Guid UserId);

/// <summary>SPEC-007 AC-22: <c>GET /api/v1/account</c>.</summary>
public sealed record AccountSummaryResponse(
    int OwnedStorages,
    int OwnedSharedStorages,
    int Memberships
)
{
    public static AccountSummaryResponse From(AccountSummary summary) =>
        new(summary.OwnedStorages, summary.OwnedSharedStorages, summary.Memberships);
}
