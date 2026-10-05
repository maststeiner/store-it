using StoreIt.Domain;

namespace StoreIt.Application;

/// <summary>Item count and member count of one storage, as the statistics query reports it.</summary>
public sealed record StorageShape(int ItemCount, int MemberCount);

/// <summary>The item facts the statistics need — no name, no amount, no owner.</summary>
public sealed record ItemShape(Unit Unit, DateOnly? ExpiryDate, bool HasProductionDate);

/// <summary>
/// SPEC-008 D7: read-side port for the operator statistics. Deliberately separate from the
/// aggregate repositories, which are scoped to the signed-in user (SPEC-003 ownership filter):
/// these queries see every user, storage and item and return shapes, never entities.
/// </summary>
public interface IUsageStatisticsQuery
{
    Task<int> CountUsersAsync(CancellationToken cancellationToken);

    Task<int> CountUsersCreatedSinceAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, int>> CountUsersByIssuerAsync(
        CancellationToken cancellationToken
    );

    /// <summary>Users that neither own nor are a member of any storage.</summary>
    Task<int> CountUsersWithoutAnyStorageAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StorageShape>> GetStorageShapesAsync(CancellationToken cancellationToken);

    /// <summary>Invitations whose link still works at <paramref name="now"/>.</summary>
    Task<int> CountPendingInvitationsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<ItemShape>> GetItemShapesAsync(CancellationToken cancellationToken);
}

public sealed record UserStatistics(
    int Total,
    int NewLast7Days,
    int NewLast30Days,
    IReadOnlyDictionary<string, int> ByIssuer,
    int WithoutAnyStorage
);

public sealed record StorageStatistics(
    int Total,
    int Shared,
    int Empty,
    int ItemsPerStorageMedian,
    int ItemsPerStorageMax
);

public sealed record InvitationStatistics(int Pending);

public sealed record ItemStatistics(
    int Total,
    int WithExpiryDate,
    int WithProductionDate,
    int Expired,
    int ExpiringSoon,
    IReadOnlyDictionary<string, int> ByUnit
);

/// <summary>SPEC-008 AC-03…AC-06: aggregates only — no names, e-mails or ids.</summary>
public sealed record UsageStatistics(
    DateTimeOffset GeneratedAt,
    UserStatistics Users,
    StorageStatistics Storages,
    InvitationStatistics Invitations,
    ItemStatistics Items
);

/// <summary>
/// SPEC-008 D4: computes the operator statistics on request. "New in the last N days" counts
/// calendar days in UTC including today (N = 7 → today and the six days before it); the
/// expiry buckets apply <see cref="ExpiryRules"/> to the app's current UTC date, so the
/// dashboard counts exactly what the storage views show.
/// </summary>
public sealed class GetUsageStatisticsUseCase(
    IUsageStatisticsQuery query,
    TimeProvider timeProvider
)
{
    public async Task<UsageStatistics> ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var users = new UserStatistics(
            await query.CountUsersAsync(cancellationToken),
            await query.CountUsersCreatedSinceAsync(StartOfWindow(today, 7), cancellationToken),
            await query.CountUsersCreatedSinceAsync(StartOfWindow(today, 30), cancellationToken),
            await query.CountUsersByIssuerAsync(cancellationToken),
            await query.CountUsersWithoutAnyStorageAsync(cancellationToken)
        );

        var storageShapes = await query.GetStorageShapesAsync(cancellationToken);
        var itemCounts = storageShapes.Select(s => s.ItemCount).ToList();
        var storages = new StorageStatistics(
            storageShapes.Count,
            storageShapes.Count(s => s.MemberCount > 0),
            storageShapes.Count(s => s.ItemCount == 0),
            Median(itemCounts),
            itemCounts.Count == 0 ? 0 : itemCounts.Max()
        );

        var invitations = new InvitationStatistics(
            await query.CountPendingInvitationsAsync(now, cancellationToken)
        );

        var itemShapes = await query.GetItemShapesAsync(cancellationToken);
        var statuses = itemShapes.Select(i => ExpiryRules.GetStatus(i.ExpiryDate, today)).ToList();
        var items = new ItemStatistics(
            itemShapes.Count,
            itemShapes.Count(i => i.ExpiryDate is not null),
            itemShapes.Count(i => i.HasProductionDate),
            statuses.Count(s => s == ExpiryStatus.Expired),
            statuses.Count(s => s == ExpiryStatus.ExpiringSoon),
            itemShapes
                .GroupBy(i => i.Unit.ToString(), StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal)
        );

        return new UsageStatistics(now, users, storages, invitations, items);
    }

    /// <summary>00:00 UTC of the first of the last <paramref name="days"/> calendar days.</summary>
    private static DateTimeOffset StartOfWindow(DateOnly today, int days) =>
        new(today.AddDays(1 - days).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    /// <summary>Lower median (the smaller middle value for an even count); 0 for no storages.</summary>
    private static int Median(List<int> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        return values[(values.Count - 1) / 2];
    }
}
