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
    ExpiryStatus ExpiryStatus,
    IReadOnlyList<string> Tags
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
            itemWithStatus.Status,
            itemWithStatus.Tags
        );
}

/// <summary>SPEC-011 AC-05: a storage tag for suggestions and the active filter.</summary>
public sealed record TagResponse(string Name, int ItemCount)
{
    public static TagResponse From(TagWithCount tag) => new(tag.Name, tag.ItemCount);
}

public sealed record StorageRequest(string Name);

/// <summary>SPEC-011 D9/EC-07: <c>Tags</c> travels with the item; absent means none.</summary>
public sealed record ItemRequest(
    string Name,
    decimal Amount,
    Unit Unit,
    DateOnly? ExpiryDate,
    DateOnly? ProductionDate,
    IReadOnlyList<string?>? Tags = null
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

// --- SPEC-008 operator statistics: GET /api/v1/admin/statistics (aggregates only, AC-06) ---

public sealed record UserStatisticsResponse(
    int Total,
    int NewLast7Days,
    int NewLast30Days,
    IReadOnlyDictionary<string, int> ByIssuer,
    int WithoutAnyStorage
);

public sealed record StorageStatisticsResponse(
    int Total,
    int Shared,
    int Empty,
    int ItemsPerStorageMedian,
    int ItemsPerStorageMax
);

public sealed record InvitationStatisticsResponse(int Pending);

public sealed record ItemStatisticsResponse(
    int Total,
    int WithExpiryDate,
    int WithProductionDate,
    int Expired,
    int ExpiringSoon,
    IReadOnlyDictionary<string, int> ByUnit
);

public sealed record UsageStatisticsResponse(
    DateTimeOffset GeneratedAt,
    UserStatisticsResponse Users,
    StorageStatisticsResponse Storages,
    InvitationStatisticsResponse Invitations,
    ItemStatisticsResponse Items
)
{
    public static UsageStatisticsResponse From(UsageStatistics statistics) =>
        new(
            statistics.GeneratedAt,
            new UserStatisticsResponse(
                statistics.Users.Total,
                statistics.Users.NewLast7Days,
                statistics.Users.NewLast30Days,
                statistics.Users.ByIssuer,
                statistics.Users.WithoutAnyStorage
            ),
            new StorageStatisticsResponse(
                statistics.Storages.Total,
                statistics.Storages.Shared,
                statistics.Storages.Empty,
                statistics.Storages.ItemsPerStorageMedian,
                statistics.Storages.ItemsPerStorageMax
            ),
            new InvitationStatisticsResponse(statistics.Invitations.Pending),
            new ItemStatisticsResponse(
                statistics.Items.Total,
                statistics.Items.WithExpiryDate,
                statistics.Items.WithProductionDate,
                statistics.Items.Expired,
                statistics.Items.ExpiringSoon,
                statistics.Items.ByUnit
            )
        );
}

// SPEC-009 about

/// <summary>One shipped third-party package of the API (SPEC-009 AC-09).</summary>
public sealed record ThirdPartyComponentResponse(
    string Name,
    string Version,
    string License,
    string? Copyright,
    string? Url,
    string? Text
)
{
    public static ThirdPartyComponentResponse From(ThirdPartyComponent component) =>
        new(
            component.Name,
            component.Version,
            component.License,
            component.Copyright,
            component.Url,
            component.Text
        );
}

/// <summary>
/// SPEC-009 AC-04: the running API's release version (<c>v0.3.0</c> or <c>dev</c>), commit,
/// .NET runtime and third-party packages.
/// </summary>
public sealed record AboutResponse(
    string Version,
    string? Revision,
    string Runtime,
    IReadOnlyList<ThirdPartyComponentResponse> Components
)
{
    public static AboutResponse From(IAboutInformation about) =>
        new(
            about.Version.Version,
            about.Version.Revision,
            about.Runtime,
            about.Components.Select(ThirdPartyComponentResponse.From).ToList()
        );
}
