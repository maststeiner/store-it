using Microsoft.EntityFrameworkCore;
using StoreIt.Application;

namespace StoreIt.Infrastructure;

/// <summary>
/// SPEC-008 D7: the operator statistics read side. Every query ignores the ownership filter
/// of <see cref="StoreItDbContext"/> on purpose — the operator counts all storages, not the
/// caller's — and projects to shapes, so no entity leaves this class.
/// </summary>
public sealed class UsageStatisticsQuery(StoreItDbContext dbContext) : IUsageStatisticsQuery
{
    public Task<int> CountUsersAsync(CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().CountAsync(cancellationToken);

    public Task<int> CountUsersCreatedSinceAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken
    ) => dbContext.Users.AsNoTracking().CountAsync(u => u.CreatedAt >= since, cancellationToken);

    public async Task<IReadOnlyDictionary<string, int>> CountUsersByIssuerAsync(
        CancellationToken cancellationToken
    )
    {
        var rows = await dbContext
            .Users.AsNoTracking()
            .GroupBy(u => u.Issuer)
            .Select(g => new { Issuer = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return rows.OrderBy(r => r.Issuer, StringComparer.Ordinal)
            .ToDictionary(r => r.Issuer, r => r.Count, StringComparer.Ordinal);
    }

    public Task<int> CountUsersWithoutAnyStorageAsync(CancellationToken cancellationToken) =>
        dbContext
            .Users.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(
                u =>
                    !dbContext.Storages.Any(s =>
                        s.OwnerId == u.Id || s.Members.Any(m => m.UserId == u.Id)
                    ),
                cancellationToken
            );

    public async Task<IReadOnlyList<StorageShape>> GetStorageShapesAsync(
        CancellationToken cancellationToken
    ) =>
        await dbContext
            .Storages.AsNoTracking()
            .IgnoreQueryFilters()
            .Select(s => new StorageShape(s.Items.Count, s.Members.Count))
            .ToListAsync(cancellationToken);

    public Task<int> CountPendingInvitationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken
    ) =>
        dbContext
            .StorageInvitations.AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(i => i.ExpiresAt > now, cancellationToken);

    public async Task<IReadOnlyList<ItemShape>> GetItemShapesAsync(
        CancellationToken cancellationToken
    ) =>
        await dbContext
            .Storages.AsNoTracking()
            .IgnoreQueryFilters()
            .SelectMany(s => s.Items)
            .Select(i => new ItemShape(i.Unit, i.ExpiryDate, i.ProductionDate != null))
            .ToListAsync(cancellationToken);
}
