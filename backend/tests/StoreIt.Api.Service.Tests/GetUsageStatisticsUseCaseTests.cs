using Microsoft.Extensions.Time.Testing;
using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// SPEC-008 D4 — the arithmetic of the statistics, against a scripted query port:
/// calendar-day windows (UTC, inclusive of today), the lower median, the expiry buckets via
/// <see cref="ExpiryRules"/>, and the empty-database case (EC-03).
/// </summary>
public class GetUsageStatisticsUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Execute_EmptyDatabase_ReturnsZerosAndEmptyMaps()
    {
        var useCase = new GetUsageStatisticsUseCase(new FakeQuery(), new FakeTimeProvider(Now));

        var stats = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(Now, stats.GeneratedAt);
        Assert.Equal(new UserStatistics(0, 0, 0, Empty, 0), stats.Users with { ByIssuer = Empty });
        Assert.Empty(stats.Users.ByIssuer);
        Assert.Equal(new StorageStatistics(0, 0, 0, 0, 0), stats.Storages);
        Assert.Equal(0, stats.Invitations.Pending);
        Assert.Equal(0, stats.Items.Total);
        Assert.Empty(stats.Items.ByUnit);
    }

    [Fact]
    public async Task Execute_Windows_StartAtMidnightUtcOfTheFirstCalendarDay()
    {
        var query = new FakeQuery();
        var useCase = new GetUsageStatisticsUseCase(query, new FakeTimeProvider(Now));

        await useCase.ExecuteAsync(CancellationToken.None);

        // 15 June, 7 days inclusive → from 9 June 00:00 UTC; 30 days → from 17 May 00:00 UTC.
        Assert.Equal(
            [
                new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 5, 17, 0, 0, 0, TimeSpan.Zero),
            ],
            query.SinceArguments
        );
        Assert.Equal(Now, query.PendingNowArgument);
    }

    [Fact]
    public async Task Execute_Storages_CountsSharedAndEmptyAndUsesLowerMedian()
    {
        var query = new FakeQuery
        {
            Storages =
            [
                new StorageShape(5, 1),
                new StorageShape(0, 0),
                new StorageShape(1, 0),
                new StorageShape(2, 3),
            ],
        };
        var useCase = new GetUsageStatisticsUseCase(query, new FakeTimeProvider(Now));

        var stats = await useCase.ExecuteAsync(CancellationToken.None);

        // sorted item counts 0,1,2,5 → lower median 1; max 5; shared = member count > 0.
        Assert.Equal(new StorageStatistics(4, 2, 1, 1, 5), stats.Storages);
    }

    [Fact]
    public async Task Execute_Items_BucketsByExpiryRulesAndGroupsByUnit()
    {
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        var query = new FakeQuery
        {
            Items =
            [
                new ItemShape(Unit.Piece, today.AddDays(-1), false), // expired
                new ItemShape(Unit.Piece, today, false), // expiring soon (0 days)
                new ItemShape(
                    Unit.Liter,
                    today.AddDays(ExpiryRules.ExpiringSoonThresholdDays),
                    true
                ),
                new ItemShape(
                    Unit.Liter,
                    today.AddDays(ExpiryRules.ExpiringSoonThresholdDays + 1),
                    false
                ),
                new ItemShape(Unit.Gram, null, true),
            ],
        };
        var useCase = new GetUsageStatisticsUseCase(query, new FakeTimeProvider(Now));

        var stats = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(5, stats.Items.Total);
        Assert.Equal(4, stats.Items.WithExpiryDate);
        Assert.Equal(2, stats.Items.WithProductionDate);
        Assert.Equal(1, stats.Items.Expired);
        Assert.Equal(2, stats.Items.ExpiringSoon);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["Gram"] = 1,
                ["Liter"] = 2,
                ["Piece"] = 2,
            },
            stats.Items.ByUnit
        );
    }

    [Fact]
    public async Task Execute_Users_PassesThroughCountsAndIssuers()
    {
        var query = new FakeQuery
        {
            Users = 7,
            UsersSince = [2, 5],
            ByIssuer = new Dictionary<string, int> { ["Google"] = 3, ["Microsoft"] = 4 },
            UsersWithoutStorage = 2,
            PendingInvitations = 3,
        };
        var useCase = new GetUsageStatisticsUseCase(query, new FakeTimeProvider(Now));

        var stats = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(7, stats.Users.Total);
        Assert.Equal(2, stats.Users.NewLast7Days);
        Assert.Equal(5, stats.Users.NewLast30Days);
        Assert.Equal(2, stats.Users.WithoutAnyStorage);
        Assert.Equal(4, stats.Users.ByIssuer["Microsoft"]);
        Assert.Equal(3, stats.Invitations.Pending);
    }

    private static readonly IReadOnlyDictionary<string, int> Empty = new Dictionary<string, int>();

    private sealed class FakeQuery : IUsageStatisticsQuery
    {
        public int Users { get; init; }
        public int[] UsersSince { get; init; } = [0, 0];
        public IReadOnlyDictionary<string, int> ByIssuer { get; init; } = Empty;
        public int UsersWithoutStorage { get; init; }
        public IReadOnlyList<StorageShape> Storages { get; init; } = [];
        public int PendingInvitations { get; init; }
        public IReadOnlyList<ItemShape> Items { get; init; } = [];

        public List<DateTimeOffset> SinceArguments { get; } = [];
        public DateTimeOffset? PendingNowArgument { get; private set; }

        public Task<int> CountUsersAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Users);

        public Task<int> CountUsersCreatedSinceAsync(
            DateTimeOffset since,
            CancellationToken cancellationToken
        )
        {
            SinceArguments.Add(since);
            return Task.FromResult(UsersSince[SinceArguments.Count - 1]);
        }

        public Task<IReadOnlyDictionary<string, int>> CountUsersByIssuerAsync(
            CancellationToken cancellationToken
        ) => Task.FromResult(ByIssuer);

        public Task<int> CountUsersWithoutAnyStorageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(UsersWithoutStorage);

        public Task<IReadOnlyList<StorageShape>> GetStorageShapesAsync(
            CancellationToken cancellationToken
        ) => Task.FromResult(Storages);

        public Task<int> CountPendingInvitationsAsync(
            DateTimeOffset now,
            CancellationToken cancellationToken
        )
        {
            PendingNowArgument = now;
            return Task.FromResult(PendingInvitations);
        }

        public Task<IReadOnlyList<ItemShape>> GetItemShapesAsync(
            CancellationToken cancellationToken
        ) => Task.FromResult(Items);
    }
}
