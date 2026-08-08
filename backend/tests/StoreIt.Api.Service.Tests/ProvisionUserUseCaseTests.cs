using Microsoft.Extensions.Time.Testing;
using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// Plain unit tests for <see cref="ProvisionUserUseCase"/>.
/// Uses an in-memory fake repository and FakeTimeProvider — no WebApplicationFactory,
/// no Docker, no database.
/// </summary>
public sealed class ProvisionUserUseCaseTests
{
    // ──────────────────────────────── helpers ────────────────────────────────

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _store = [];
        private bool _throwOnNextSave;
        private User? _winnerAfterRace;

        /// <summary>Number of successful <see cref="SaveChangesAsync"/> calls.</summary>
        public int SaveCount { get; private set; }

        /// <summary>
        /// Configure the repo to throw <see cref="UserAlreadyExistsException"/> on the next
        /// <see cref="SaveChangesAsync"/> call, then return <paramref name="winner"/> on the
        /// <em>subsequent</em> <see cref="GetBySubjectAsync"/> call (the reload in the catch
        /// block).
        /// </summary>
        public void SimulateRaceWith(User winner)
        {
            _throwOnNextSave = true;
            _winnerAfterRace = winner;
        }

        public Task<User?> GetBySubjectAsync(
            string issuer,
            string subject,
            CancellationToken cancellationToken
        )
        {
            // If a race winner is queued (i.e., the exception has already been thrown and we are
            // in the catch-block reload), return the winner and clear the queue.
            if (_winnerAfterRace is not null && !_throwOnNextSave)
            {
                var w = _winnerAfterRace;
                _winnerAfterRace = null;
                return Task.FromResult<User?>(w);
            }

            var user = _store.FirstOrDefault(u => u.Issuer == issuer && u.Subject == subject);
            return Task.FromResult(user);
        }

        public void Add(User user) => _store.Add(user);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (_throwOnNextSave)
            {
                _throwOnNextSave = false;
                throw new UserAlreadyExistsException();
            }

            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private static (
        ProvisionUserUseCase useCase,
        FakeUserRepository repo,
        FakeTimeProvider clock
    ) Build()
    {
        var repo = new FakeUserRepository();
        var clock = new FakeTimeProvider();
        var useCase = new ProvisionUserUseCase(repo, clock);
        return (useCase, repo, clock);
    }

    // ──────────────────────────────── tests ──────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FirstLogin_CreatesUser()
    {
        var (useCase, _, _) = Build();

        var user = await useCase.ExecuteAsync(
            issuer: "https://idp.example.com",
            subject: "sub-001",
            email: "alice@example.com",
            displayName: "Alice",
            cancellationToken: default
        );

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("https://idp.example.com", user.Issuer);
        Assert.Equal("sub-001", user.Subject);
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal("Alice", user.DisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_SecondLogin_ReusesUserAndRefreshesProfile()
    {
        var (useCase, repo, _) = Build();

        // First login
        var first = await useCase.ExecuteAsync(
            issuer: "https://idp.example.com",
            subject: "sub-001",
            email: "alice@example.com",
            displayName: "Alice",
            cancellationToken: default
        );
        var savesAfterFirst = repo.SaveCount;

        // Second login with updated profile fields
        var second = await useCase.ExecuteAsync(
            issuer: "https://idp.example.com",
            subject: "sub-001",
            email: "alice-new@example.com",
            displayName: "Alice Updated",
            cancellationToken: default
        );

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("alice-new@example.com", second.Email);
        Assert.Equal("Alice Updated", second.DisplayName);
        // The fake returns the stored instance, so equal fields alone cannot prove the
        // update path ran. Assert the second login actually persisted (SaveChangesAsync
        // called again) — that is what the "refresh profile" behaviour hinges on.
        Assert.Equal(savesAfterFirst + 1, repo.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_DifferentIssuers_ProduceSeparateUsers()
    {
        var (useCase, _, _) = Build();

        var user1 = await useCase.ExecuteAsync(
            issuer: "https://idp-a.example.com",
            subject: "sub-001",
            email: "alice@idp-a.com",
            displayName: "Alice A",
            cancellationToken: default
        );

        var user2 = await useCase.ExecuteAsync(
            issuer: "https://idp-b.example.com",
            subject: "sub-001",
            email: "alice@idp-b.com",
            displayName: "Alice B",
            cancellationToken: default
        );

        Assert.NotEqual(user1.Id, user2.Id);
        Assert.Equal("https://idp-a.example.com", user1.Issuer);
        Assert.Equal("https://idp-b.example.com", user2.Issuer);
    }

    [Fact]
    public async Task ExecuteAsync_InsertRace_ReturnsWinnerWithUpdatedProfile()
    {
        var (useCase, repo, clock) = Build();

        // Create the "winner" that the other concurrent request already persisted with its
        // original profile. The retry call will supply DIFFERENT values — the use case must
        // apply UpdateProfile on the recovered winner and persist the refreshed data.
        var winner = User.Create(
            issuer: "https://idp.example.com",
            subject: "sub-001",
            email: "alice@example.com",
            displayName: "Alice",
            createdAt: clock.GetUtcNow()
        );

        // Configure the repo: throw on save (simulating the unique-key violation),
        // then return `winner` on the subsequent GetBySubjectAsync.
        repo.SimulateRaceWith(winner);

        // Retry with a changed profile (e.g. the IdP updated the user's display name).
        var result = await useCase.ExecuteAsync(
            issuer: "https://idp.example.com",
            subject: "sub-001",
            email: "alice-updated@example.com",
            displayName: "Alice Updated",
            cancellationToken: default
        );

        // The returned user must be the winner's record.
        Assert.Equal(winner.Id, result.Id);
        // The profile must reflect the values from THIS call, not the winner's original data.
        Assert.Equal("alice-updated@example.com", result.Email);
        Assert.Equal("Alice Updated", result.DisplayName);
        // The updated profile must have been persisted (SaveChangesAsync called once for the
        // profile update after the race recovery — the first save attempt threw instead).
        Assert.Equal(1, repo.SaveCount);
    }
}
