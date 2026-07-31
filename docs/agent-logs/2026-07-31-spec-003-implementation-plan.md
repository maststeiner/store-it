# Accounts & Storage Ownership Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add federated login (Microsoft + Google via direct OIDC, BFF session) and per-storage ownership so each user sees only the storages/items they created.

**Architecture:** The .NET API becomes a Backend-for-Frontend: OpenID Connect handlers (one per provider) do the code+PKCE exchange server-side and issue an HttpOnly cookie session — no tokens in the browser. A local `User` is JIT-provisioned on first login, keyed by `(Issuer, Subject)`. Ownership is enforced centrally by an EF Core global query filter on `Storage`, so cross-user reads return nothing → map to 404. The Angular app gains a login screen, a route guard, a 401 interceptor and a session header. Implements SPEC-003; realises ADR-004.

**Tech Stack:** .NET 10 (minimal APIs, EF Core 10 + Npgsql, cookie + OpenIdConnect auth), Angular 22 (standalone, signals, functional guards/interceptors), xUnit + Testcontainers.PostgreSql + `WebApplicationFactory`, Vitest + Playwright.

## Global Constraints

- **Layering (ADR-001):** Domain has no external deps; Application → Domain only; Infrastructure → Application+Domain; Api → Application+Domain. **Only `Program.cs` may reference Infrastructure** (for DI). Ownership/authorization logic lives in Application/Infrastructure — never in Api handlers or the frontend. Enforced by `StoreIt.Architecture.Tests`.
- **API-first (ADR-002) + contract gate (ADR-006):** endpoints stay under `/api/v1/**`; the OpenAPI document `backend/openapi/StoreIt.Api.json` is generated on build and committed — every contract change is a reviewed diff. Path stays `v1` (requiring auth is a deliberate behavioural change, no `v2`).
- **Build is strict:** `net10.0`, `Nullable=enable`, `CodeAnalysisTreatWarningsAsErrors=true` — any analyzer warning fails the build/CI.
- **Central Package Management:** add every NuGet version to `backend/Directory.Packages.props` (`<PackageVersion>`), reference without version in the `.csproj`.
- **Persistence (ADR-003):** PostgreSQL + EF Core; domain generates GUID keys (`ValueGeneratedNever`); entity config via `IEntityTypeConfiguration` auto-applied.
- **Secrets:** OIDC client id/secret per provider come from the environment (12-factor); **never commit them** (a `PreToolUse` guardrail also blocks secrets).
- **Authorization semantics:** unauthenticated on `/api/v1/**` → **401**; cross-user access by id → **404** (existence not disclosed); `GET /health` stays open with no IdP dependency.
- **Frontend:** Angular standalone + signals; no new i18n library (use the in-house `TranslateService` + `translate` pipe); all user-facing strings in **de/en/fr/it**; functional guards/interceptors via `withInterceptors`/`CanActivateFn`.
- **Tests are derived from the ACs (QA persona), not from the code.** TDD: failing test first.
- **Branch/PR (two-stage):** the frozen baseline (SPEC-003 + ADR-004 + this plan) ships as its **own docs PR** on `feature/spec-003-auth` → `develop` first, so it is reviewed (incl. CodeRabbit) and merged as the shared baseline before any code exists. **Implementation then happens on a fresh `feature/spec-003-auth-impl` branch cut from the updated `develop`** (own worktree). All commits are local; **do not push** — the human pushes, opens each PR to `develop`, and merges (Gates G2/G3). DB/schema-migration commands are Approval-tier.

---

## AC → Task coverage map

| AC / EC | Task(s) |
|---------|---------|
| AC-01 unauthenticated → 401 | 8, 10 |
| AC-02 login flow → session | 8 |
| AC-03 JIT provisioning (first login) | 4, 8 |
| AC-04 known user reused | 4 |
| AC-05 `/auth/me` (authed / 401) | 8 |
| AC-06 logout ends session | 8 |
| AC-07 `/health` open | 10 |
| AC-08 create assigns OwnerId server-side | 2, 10 |
| AC-09 list only own storages | 6, 10 |
| AC-10 cross-user by id → 404 | 6, 10 |
| AC-11 items scoped via storage | 6, 10 |
| AC-12 SPEC-001 unchanged for owner | 10 |
| EC-01 expired session → 401 → login | 14 |
| EC-02 no email still provisions | 1, 4 |
| EC-03 two providers → two users | 1, 4 |
| EC-04 invalid code / state → abort | 8 (framework default) + manual verify |
| Global query filter isolates at repo level | 6 |
| Frontend login / guard / session / i18n | 13–17 |
| E2E logged-in flow | 18 |
| Threat-model R-06 update | 12 |

---

## File Structure

**Backend — new**
- `backend/src/StoreIt.Domain/User.cs` — account entity (Id, Issuer, Subject, Email, DisplayName, CreatedAt).
- `backend/src/StoreIt.Application/ICurrentUser.cs` — port exposing the current local `User.Id`.
- `backend/src/StoreIt.Application/IUserRepository.cs` — find/add users.
- `backend/src/StoreIt.Application/UserUseCases.cs` — `ProvisionUserUseCase`.
- `backend/src/StoreIt.Infrastructure/UserConfiguration.cs` — EF mapping + unique `(Issuer, Subject)`.
- `backend/src/StoreIt.Infrastructure/UserRepository.cs`.
- `backend/src/StoreIt.Infrastructure/DesignTimeDbContextFactory.cs` — design-time DbContext for `dotnet ef` (null current user).
- `backend/src/StoreIt.Api/CurrentUser.cs` — `HttpContext`-backed `ICurrentUser`.
- `backend/src/StoreIt.Api/AuthEndpoints.cs` — `/auth/login/{provider}`, `/auth/callback/{provider}`, `/auth/logout`, `/auth/me`.
- `backend/src/StoreIt.Api/AuthenticationSetup.cs` — `AddStoreItAuthentication(...)` extension (cookie + per-provider OIDC + provisioning hook).
- `backend/tests/StoreIt.Api.Service.Tests/TestAuthHandler.cs` — test auth scheme to act as arbitrary users.

**Backend — modified**
- `backend/src/StoreIt.Domain/Storage.cs` — add `OwnerId`, change `Create(name, ownerId)`.
- `backend/src/StoreIt.Infrastructure/StorageConfiguration.cs` — `OwnerId` column + FK to users.
- `backend/src/StoreIt.Infrastructure/StoreItDbContext.cs` — `DbSet<User>`, inject `ICurrentUser`, global query filter on `Storage`.
- `backend/src/StoreIt.Infrastructure/InfrastructureServiceCollectionExtensions.cs` — register `IUserRepository`.
- `backend/src/StoreIt.Application/StorageUseCases.cs` — `CreateStorageUseCase` sets `OwnerId` from `ICurrentUser`.
- `backend/src/StoreIt.Application/ApplicationServiceCollectionExtensions.cs` — register `ProvisionUserUseCase`.
- `backend/src/StoreIt.Api/Program.cs` — auth middleware, `RequireAuthorization`, map auth endpoints.
- `backend/src/StoreIt.Api/StorageEndpoints.cs` — `.RequireAuthorization()` + `ProducesProblem(401)`.
- `backend/Directory.Packages.props` — auth package versions.
- `backend/src/StoreIt.Api/StoreIt.Api.csproj` — auth package refs.
- `backend/src/StoreIt.Infrastructure/Migrations/*` — new migration.
- `backend/openapi/StoreIt.Api.json` — regenerated contract.
- `docs/security/threat-model.md` — R-06 → mitigated.

**Frontend — new**
- `frontend/src/app/core/auth.service.ts` — session signal, `loadMe`, `login(provider)`, `logout`.
- `frontend/src/app/core/auth.interceptor.ts` — 401 → clear session + redirect `/login`.
- `frontend/src/app/core/auth.guard.ts` — `CanActivateFn` protecting app routes.
- `frontend/src/app/auth/login-page.ts` (+ `.html`) — provider buttons.

**Frontend — modified**
- `frontend/src/app/app.config.ts` — `withInterceptors([authInterceptor])`.
- `frontend/src/app/app.routes.ts` — `/login` + guard on storages routes.
- `frontend/src/app/app.ts` + `app.html` — session header + logout.
- `frontend/public/assets/i18n/{de,en,fr,it}.json` — `auth.*` + `errors.auth.*` keys.
- `frontend/proxy.conf.json` — proxy `/auth` to the backend.
- `frontend/e2e/auth.spec.ts` (new) + dev-login hook (Task 18).

---

# PHASE 1 — Backend

### Task 1: `User` domain entity

**Files:**
- Create: `backend/src/StoreIt.Domain/User.cs`
- Test: `backend/tests/StoreIt.Domain.Tests/UserTests.cs`

**Interfaces:**
- Produces: `User.Create(string issuer, string subject, string? email, string? displayName, DateTimeOffset createdAt) : User`; instance props `Guid Id`, `string Issuer`, `string Subject`, `string? Email`, `string? DisplayName`, `DateTimeOffset CreatedAt`; `void UpdateProfile(string? email, string? displayName)`.

- [ ] **Step 1: Write the failing test**
```csharp
using StoreIt.Domain;
using Xunit;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidClaims_SetsFieldsAndGeneratesId()
    {
        var user = User.Create("https://issuer", "sub-123", "a@example.com", "Alex", Now);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("https://issuer", user.Issuer);
        Assert.Equal("sub-123", user.Subject);
        Assert.Equal("a@example.com", user.Email);
        Assert.Equal("Alex", user.DisplayName);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void Create_WithNoEmail_IsAllowed() // EC-02
    {
        var user = User.Create("https://issuer", "sub-123", email: null, displayName: null, Now);
        Assert.Null(user.Email);
    }

    [Theory]
    [InlineData("", "sub", "user.issuer.empty")]
    [InlineData("iss", "", "user.subject.empty")]
    public void Create_WithMissingIssuerOrSubject_Throws(string issuer, string subject, string code)
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => User.Create(issuer, subject, null, null, Now));
        Assert.Equal(code, ex.ErrorCode);
    }

    [Fact]
    public void UpdateProfile_ChangesEmailAndDisplayName()
    {
        var user = User.Create("iss", "sub", "old@x.com", "Old", Now);
        user.UpdateProfile("new@x.com", "New");
        Assert.Equal("new@x.com", user.Email);
        Assert.Equal("New", user.DisplayName);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test backend/tests/StoreIt.Domain.Tests --filter FullyQualifiedName~UserTests`
Expected: FAIL — `User` does not exist.

- [ ] **Step 3: Write minimal implementation**
```csharp
namespace StoreIt.Domain;

public class User
{
    public Guid Id { get; private set; }
    public string Issuer { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? DisplayName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { } // EF

    public static User Create(string issuer, string subject, string? email, string? displayName, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(issuer))
            throw new DomainValidationException("user.issuer.empty", "Issuer must not be empty.");
        if (string.IsNullOrWhiteSpace(subject))
            throw new DomainValidationException("user.subject.empty", "Subject must not be empty.");

        return new User
        {
            Id = Guid.NewGuid(),
            Issuer = issuer,
            Subject = subject,
            Email = email,
            DisplayName = displayName,
            CreatedAt = createdAt,
        };
    }

    public void UpdateProfile(string? email, string? displayName)
    {
        Email = email;
        DisplayName = displayName;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**
Run: `dotnet test backend/tests/StoreIt.Domain.Tests --filter FullyQualifiedName~UserTests`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add backend/src/StoreIt.Domain/User.cs backend/tests/StoreIt.Domain.Tests/UserTests.cs
git commit -m "feat(backend): add User domain entity"
```

---

### Task 2: `Storage` ownership (domain)

**Files:**
- Modify: `backend/src/StoreIt.Domain/Storage.cs`
- Modify: `backend/tests/StoreIt.Domain.Tests/StorageTests.cs` (existing `Create` call sites)
- Test: `backend/tests/StoreIt.Domain.Tests/StorageTests.cs`

**Interfaces:**
- Produces: `Storage.Create(string name, Guid ownerId) : Storage`; new prop `Guid OwnerId`.
- Consumes: nothing new.

- [ ] **Step 1: Write the failing test** (add to `StorageTests`)
```csharp
[Fact]
public void Create_WithOwner_SetsOwnerId()
{
    var owner = Guid.NewGuid();
    var storage = Storage.Create("Pantry", owner);
    Assert.Equal(owner, storage.OwnerId);
}

[Fact]
public void Create_WithEmptyOwner_Throws()
{
    var ex = Assert.Throws<DomainValidationException>(() => Storage.Create("Pantry", Guid.Empty));
    Assert.Equal("storage.owner.missing", ex.ErrorCode);
}
```

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test backend/tests/StoreIt.Domain.Tests --filter FullyQualifiedName~StorageTests`
Expected: FAIL — `Create` has one parameter / `OwnerId` missing (also existing `Create("x")` calls won't compile).

- [ ] **Step 3: Write minimal implementation**
In `Storage.cs`, add the property and change the factory (keep existing name validation):
```csharp
public Guid OwnerId { get; private set; }

public static Storage Create(string name, Guid ownerId)
{
    if (ownerId == Guid.Empty)
        throw new DomainValidationException("storage.owner.missing", "OwnerId must be provided.");
    // ... existing name validation + construction ...
    var storage = new Storage(name);
    storage.OwnerId = ownerId;
    return storage;
}
```
Then fix every existing `Storage.Create("...")` call in `StorageTests.cs` to pass an owner, e.g. `Storage.Create("Pantry", Guid.NewGuid())`.

- [ ] **Step 4: Run tests**
Run: `dotnet test backend/tests/StoreIt.Domain.Tests`
Expected: PASS (whole domain suite green).

- [ ] **Step 5: Commit**
```bash
git add backend/src/StoreIt.Domain/Storage.cs backend/tests/StoreIt.Domain.Tests/StorageTests.cs
git commit -m "feat(backend): add per-storage ownership to Storage aggregate"
```

---

### Task 3: `ICurrentUser` port

**Files:**
- Create: `backend/src/StoreIt.Application/ICurrentUser.cs`

**Interfaces:**
- Produces: `interface ICurrentUser { Guid? UserId { get; } }` (null when anonymous / no request).

- [ ] **Step 1: Write the implementation** (no test — it is a pure abstraction consumed by later tasks)
```csharp
namespace StoreIt.Application;

/// <summary>The authenticated caller's local user id, or null when anonymous.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}
```

- [ ] **Step 2: Build to verify it compiles**
Run: `dotnet build backend/src/StoreIt.Application`
Expected: success.

- [ ] **Step 3: Commit**
```bash
git add backend/src/StoreIt.Application/ICurrentUser.cs
git commit -m "feat(backend): add ICurrentUser port"
```

---

### Task 4: `IUserRepository` + `ProvisionUserUseCase`

**Files:**
- Create: `backend/src/StoreIt.Application/IUserRepository.cs`
- Create: `backend/src/StoreIt.Application/UserUseCases.cs`
- Modify: `backend/src/StoreIt.Application/ApplicationServiceCollectionExtensions.cs`
- Test: `backend/tests/StoreIt.Domain.Tests/ProvisionUserUseCaseTests.cs` (uses an in-memory fake repo — no DB, so it lives with the fast tests)

**Interfaces:**
- Produces: `interface IUserRepository { Task<User?> GetBySubjectAsync(string issuer, string subject, CancellationToken ct); void Add(User user); Task SaveChangesAsync(CancellationToken ct); }`
- Produces: `ProvisionUserUseCase.ExecuteAsync(string issuer, string subject, string? email, string? displayName, CancellationToken ct) : Task<User>` (find-or-create; refresh profile on every login).
- Consumes: `User` (Task 1), `TimeProvider` (already registered in Application DI).

- [ ] **Step 1: Write the failing test**
```csharp
using StoreIt.Application;
using StoreIt.Domain;
using Microsoft.Extensions.Time.Testing; // FakeTimeProvider
using Xunit;

public class ProvisionUserUseCaseTests
{
    private sealed class FakeUserRepo : IUserRepository
    {
        public readonly List<User> Users = [];
        public Task<User?> GetBySubjectAsync(string issuer, string subject, CancellationToken ct) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Issuer == issuer && u.Subject == subject));
        public void Add(User user) => Users.Add(user);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly FakeTimeProvider Clock =
        new(new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task FirstLogin_CreatesUser() // AC-03
    {
        var repo = new FakeUserRepo();
        var sut = new ProvisionUserUseCase(repo, Clock);

        var user = await sut.ExecuteAsync("iss", "sub-1", "a@x.com", "Alex", default);

        Assert.Single(repo.Users);
        Assert.Equal("sub-1", user.Subject);
        Assert.Equal(Clock.GetUtcNow(), user.CreatedAt);
    }

    [Fact]
    public async Task SecondLogin_ReusesUserAndRefreshesProfile() // AC-04
    {
        var repo = new FakeUserRepo();
        var sut = new ProvisionUserUseCase(repo, Clock);
        var first = await sut.ExecuteAsync("iss", "sub-1", "a@x.com", "Alex", default);

        var second = await sut.ExecuteAsync("iss", "sub-1", "new@x.com", "Alex Renamed", default);

        Assert.Single(repo.Users);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("new@x.com", second.Email);
        Assert.Equal("Alex Renamed", second.DisplayName);
    }

    [Fact]
    public async Task DifferentIssuers_ProduceSeparateUsers() // EC-03
    {
        var repo = new FakeUserRepo();
        var sut = new ProvisionUserUseCase(repo, Clock);
        await sut.ExecuteAsync("google", "sub-1", null, null, default);
        await sut.ExecuteAsync("microsoft", "sub-1", null, null, default);
        Assert.Equal(2, repo.Users.Count);
    }
}
```
> `FakeTimeProvider` comes from `Microsoft.Extensions.TimeProvider.Testing`. If it is not already referenced by `StoreIt.Domain.Tests`, add `<PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.0.0" />` to `Directory.Packages.props` and a `<PackageReference>` to the test csproj as part of this step.

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test backend/tests/StoreIt.Domain.Tests --filter FullyQualifiedName~ProvisionUserUseCaseTests`
Expected: FAIL — `IUserRepository` / `ProvisionUserUseCase` do not exist.

- [ ] **Step 3: Write minimal implementation**
`IUserRepository.cs`:
```csharp
namespace StoreIt.Application;
using StoreIt.Domain;

public interface IUserRepository
{
    Task<User?> GetBySubjectAsync(string issuer, string subject, CancellationToken cancellationToken);
    void Add(User user);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```
`UserUseCases.cs`:
```csharp
namespace StoreIt.Application;
using StoreIt.Domain;

public sealed class ProvisionUserUseCase(IUserRepository users, TimeProvider clock)
{
    public async Task<User> ExecuteAsync(
        string issuer, string subject, string? email, string? displayName, CancellationToken cancellationToken)
    {
        var existing = await users.GetBySubjectAsync(issuer, subject, cancellationToken);
        if (existing is not null)
        {
            existing.UpdateProfile(email, displayName);
            await users.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var user = User.Create(issuer, subject, email, displayName, clock.GetUtcNow());
        users.Add(user);
        await users.SaveChangesAsync(cancellationToken);
        return user;
    }
}
```
Register it in `ApplicationServiceCollectionExtensions.cs` next to the other use cases:
```csharp
services.AddScoped<ProvisionUserUseCase>();
```

- [ ] **Step 4: Run tests**
Run: `dotnet test backend/tests/StoreIt.Domain.Tests --filter FullyQualifiedName~ProvisionUserUseCaseTests`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add backend/src/StoreIt.Application backend/tests/StoreIt.Domain.Tests/ProvisionUserUseCaseTests.cs backend/Directory.Packages.props
git commit -m "feat(backend): add user provisioning use case and repository port"
```

---

### Task 5: User persistence (Infrastructure)

**Files:**
- Create: `backend/src/StoreIt.Infrastructure/UserConfiguration.cs`
- Create: `backend/src/StoreIt.Infrastructure/UserRepository.cs`
- Modify: `backend/src/StoreIt.Infrastructure/StoreItDbContext.cs` (add `DbSet<User>`)
- Modify: `backend/src/StoreIt.Infrastructure/InfrastructureServiceCollectionExtensions.cs`

**Interfaces:**
- Produces: `IUserRepository` implementation `UserRepository`; `StoreItDbContext.Users`.
- Consumes: `IUserRepository` (Task 4), `User` (Task 1).

> Verified in Task 10 end-to-end (via provisioning through the API). No standalone DB test here — folded into the authenticated service tests.

- [ ] **Step 1: Add the DbSet** to `StoreItDbContext` (full model filter added in Task 6):
```csharp
public DbSet<Storage> Storages => Set<Storage>();
public DbSet<User> Users => Set<User>();
```

- [ ] **Step 2: Write `UserConfiguration.cs`**
```csharp
namespace StoreIt.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreIt.Domain;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Issuer).IsRequired().HasMaxLength(400);
        builder.Property(u => u.Subject).IsRequired().HasMaxLength(400);
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.DisplayName).HasMaxLength(200);
        builder.Property(u => u.CreatedAt);
        builder.HasIndex(u => new { u.Issuer, u.Subject }).IsUnique();
    }
}
```

- [ ] **Step 3: Write `UserRepository.cs`**
```csharp
namespace StoreIt.Infrastructure;
using Microsoft.EntityFrameworkCore;
using StoreIt.Application;
using StoreIt.Domain;

public sealed class UserRepository(StoreItDbContext dbContext) : IUserRepository
{
    public Task<User?> GetBySubjectAsync(string issuer, string subject, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Issuer == issuer && u.Subject == subject, cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
```

- [ ] **Step 4: Register the repository** in `InfrastructureServiceCollectionExtensions.cs`:
```csharp
services.AddScoped<IUserRepository, UserRepository>();
```

- [ ] **Step 5: Build**
Run: `dotnet build backend/src/StoreIt.Infrastructure`
Expected: success.

- [ ] **Step 6: Commit**
```bash
git add backend/src/StoreIt.Infrastructure
git commit -m "feat(backend): persist users with unique (issuer, subject)"
```

---

### Task 6: Global query filter for ownership

**Files:**
- Modify: `backend/src/StoreIt.Infrastructure/StoreItDbContext.cs` (inject `ICurrentUser`, add filter)
- Modify: `backend/src/StoreIt.Infrastructure/StorageConfiguration.cs` (map `OwnerId` + FK)
- Create: `backend/src/StoreIt.Infrastructure/DesignTimeDbContextFactory.cs`

**Interfaces:**
- Consumes: `ICurrentUser` (Task 3), `User` (Task 1), `Storage.OwnerId` (Task 2).
- Produces: every `Storages` query is transparently filtered to `OwnerId == currentUser.UserId`.

> Behaviour is proven end-to-end in Task 10 (user A vs user B). This task wires the mechanism and keeps the design-time tooling working.

- [ ] **Step 1: Inject `ICurrentUser` and add the filter** in `StoreItDbContext.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Infrastructure;

public class StoreItDbContext(DbContextOptions<StoreItDbContext> options, ICurrentUser currentUser)
    : DbContext(options)
{
    public DbSet<Storage> Storages => Set<Storage>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoreItDbContext).Assembly);
        // Ownership isolation: anonymous (UserId == null) matches nothing.
        modelBuilder.Entity<Storage>().HasQueryFilter(s => s.OwnerId == currentUser.UserId);
    }
}
```
> Items are reachable only through the `Storage` aggregate (`Include(s => s.Items)`, no `DbSet<Item>`), so the Storage-level filter fully covers items (AC-11).

- [ ] **Step 2: Map `OwnerId` + FK** in `StorageConfiguration.cs` (add inside `Configure`)
```csharp
builder.Property(s => s.OwnerId).IsRequired();
builder.HasOne<User>()
       .WithMany()
       .HasForeignKey(s => s.OwnerId)
       .IsRequired()
       .OnDelete(DeleteBehavior.Cascade);
builder.HasIndex(s => s.OwnerId);
```

- [ ] **Step 3: Add the design-time factory** so `dotnet ef` can build the context without a request scope
```csharp
namespace StoreIt.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StoreIt.Application;

/// <summary>Used only by `dotnet ef` at design time. No current user → no filtering.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StoreItDbContext>
{
    private sealed class NoCurrentUser : ICurrentUser { public Guid? UserId => null; }

    public StoreItDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StoreItDbContext>()
            .UseNpgsql("Host=localhost;Database=storeit_design;Username=design;Password=design")
            .Options;
        return new StoreItDbContext(options, new NoCurrentUser());
    }
}
```

- [ ] **Step 4: Build**
Run: `dotnet build backend/src/StoreIt.Infrastructure`
Expected: success (a design-time-factory + query-filter warning-free build).

- [ ] **Step 5: Commit**
```bash
git add backend/src/StoreIt.Infrastructure
git commit -m "feat(backend): enforce storage ownership via global query filter"
```

---

### Task 7: Test authentication handler

**Files:**
- Create: `backend/tests/StoreIt.Api.Service.Tests/TestAuthHandler.cs`
- Modify: `backend/tests/StoreIt.Api.Service.Tests/ApiTestFixture.cs` (register the test scheme + a helper client)

**Interfaces:**
- Produces: a `"Test"` auth scheme that reads headers `X-Test-Issuer` / `X-Test-Subject` / `X-Test-Email` / `X-Test-Name` and builds a `ClaimsPrincipal`; `ApiTestFixture.CreateClientAs(issuer, subject, email?, name?)` returning an `HttpClient` that sends those headers.
- Consumes: the app's claim conventions (the local user id claim `sub_local` is added by the app's provisioning during the real flow; in tests the handler triggers the same provisioning path — see Step 2).

- [ ] **Step 1: Write `TestAuthHandler.cs`**
```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Subject", out var subject))
            return Task.FromResult(AuthenticateResult.NoResult()); // anonymous → 401 on protected routes

        var issuer = Request.Headers["X-Test-Issuer"].FirstOrDefault() ?? "test-issuer";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject!),
            new("iss", issuer),
        };
        if (Request.Headers.TryGetValue("X-Test-Email", out var email))
            claims.Add(new Claim(ClaimTypes.Email, email!));
        if (Request.Headers.TryGetValue("X-Test-Name", out var name))
            claims.Add(new Claim(ClaimTypes.Name, name!));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

- [ ] **Step 2: Override auth + provisioning in `ApiTestFixture.ConfigureWebHost`** so the `"Test"` scheme is the default and every request provisions/loads the local user (mirrors the real `OnTokenValidated` hook), making `ICurrentUser.UserId` resolve:
```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.UseSetting("ConnectionStrings:storeit", _postgres.GetConnectionString());
    builder.ConfigureTestServices(services =>
    {
        services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        // Resolve local user id from the test claims on each request, same as production claims transformation.
        services.AddScoped<IClaimsTransformation, ProvisioningClaimsTransformation>();
    });
}
```
> `ProvisioningClaimsTransformation` is the shared production component built in Task 8 (it reads `iss`+`NameIdentifier`, calls `ProvisionUserUseCase`, and adds a `sub_local` claim). Reusing it here means tests exercise the real provisioning path.

- [ ] **Step 3: Add the helper client**
```csharp
public HttpClient CreateClientAs(string subject, string issuer = "test-issuer", string? email = null, string? name = null)
{
    var client = CreateClient();
    client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
    client.DefaultRequestHeaders.Add("X-Test-Issuer", issuer);
    if (email is not null) client.DefaultRequestHeaders.Add("X-Test-Email", email);
    if (name is not null) client.DefaultRequestHeaders.Add("X-Test-Name", name);
    return client;
}
```

- [ ] **Step 4: Build the test project** (it will not fully compile until Task 8 provides `ProvisioningClaimsTransformation`; that is expected — Tasks 7 and 8 land together).
Run: `dotnet build backend/tests/StoreIt.Api.Service.Tests`
Expected: FAIL referencing `ProvisioningClaimsTransformation` — resolved in Task 8.

- [ ] **Step 5: Commit** (WIP infra, compiles after Task 8)
```bash
git add backend/tests/StoreIt.Api.Service.Tests/TestAuthHandler.cs backend/tests/StoreIt.Api.Service.Tests/ApiTestFixture.cs
git commit -m "test(backend): add test auth scheme and per-user client helper"
```

---

### Task 8: Authentication wiring, provisioning transformation & auth endpoints

**Files:**
- Modify: `backend/Directory.Packages.props`, `backend/src/StoreIt.Api/StoreIt.Api.csproj` (packages)
- Create: `backend/src/StoreIt.Api/CurrentUser.cs`
- Create: `backend/src/StoreIt.Api/ProvisioningClaimsTransformation.cs`
- Create: `backend/src/StoreIt.Api/AuthenticationSetup.cs`
- Create: `backend/src/StoreIt.Api/AuthEndpoints.cs`
- Modify: `backend/src/StoreIt.Api/Program.cs`
- Modify: `backend/src/StoreIt.Api/appsettings.json` (non-secret auth shape only)
- Test: `backend/tests/StoreIt.Api.Service.Tests/AuthEndpointsTests.cs`

**Interfaces:**
- Produces: `IServiceCollection.AddStoreItAuthentication(IConfiguration)`; `IEndpointRouteBuilder.MapAuthEndpoints()`; `ProvisioningClaimsTransformation` (adds `sub_local` claim); `CurrentUser : ICurrentUser` (reads `sub_local`).
- Consumes: `ProvisionUserUseCase` (Task 4), `ICurrentUser` (Task 3), test handler (Task 7).

- [ ] **Step 1: Add packages.** In `Directory.Packages.props`:
```xml
<PackageVersion Include="Microsoft.AspNetCore.Authentication.OpenIdConnect" Version="10.0.10" />
```
In `StoreIt.Api.csproj`:
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.OpenIdConnect" />
```
> Google is configured as a second generic OpenID Connect handler against `https://accounts.google.com` — no extra package needed.

- [ ] **Step 2: Write `CurrentUser.cs`** (reads the local id claim from `HttpContext`)
```csharp
namespace StoreIt.Api;
using System.Security.Claims;
using StoreIt.Application;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string LocalIdClaim = "sub_local";

    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(LocalIdClaim), out var id) ? id : null;
}
```

- [ ] **Step 3: Write `ProvisioningClaimsTransformation.cs`** (shared by prod + tests)
```csharp
namespace StoreIt.Api;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using StoreIt.Application;

public sealed class ProvisioningClaimsTransformation(ProvisionUserUseCase provision) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return principal;
        if (principal.HasClaim(c => c.Type == CurrentUser.LocalIdClaim)) return principal;

        var issuer = principal.FindFirstValue("iss")
                     ?? principal.Identities.FirstOrDefault()?.Claims.FirstOrDefault()?.Issuer
                     ?? "unknown";
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (subject is null) return principal;

        var user = await provision.ExecuteAsync(
            issuer, subject,
            principal.FindFirstValue(ClaimTypes.Email),
            principal.FindFirstValue(ClaimTypes.Name),
            CancellationToken.None);

        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(CurrentUser.LocalIdClaim, user.Id.ToString()));
        return principal;
    }
}
```

- [ ] **Step 4: Write `AuthenticationSetup.cs`**
```csharp
namespace StoreIt.Api;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using StoreIt.Application;

public static class AuthenticationSetup
{
    public static IServiceCollection AddStoreItAuthentication(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, ProvisioningClaimsTransformation>();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = "Microsoft";
            })
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
                options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
            })
            .AddOpenIdConnect("Microsoft", options => Bind(options, config.GetSection("Authentication:Microsoft")))
            .AddOpenIdConnect("Google", options => Bind(options, config.GetSection("Authentication:Google")));

        services.AddAuthorization();
        return services;
    }

    private static void Bind(OpenIdConnectOptions options, IConfiguration section)
    {
        options.Authority = section["Authority"];
        options.ClientId = section["ClientId"];
        options.ClientSecret = section["ClientSecret"];
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = false;               // BFF: nothing goes to the browser
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.CallbackPath = section["CallbackPath"]; // e.g. /auth/callback/microsoft
    }
}
```

- [ ] **Step 5: Write `AuthEndpoints.cs`**
```csharp
namespace StoreIt.Api;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapGet("/login/{provider}", (string provider, string? returnUrl) =>
            TypedResults.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
                [MapProvider(provider)]));

        group.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return TypedResults.NoContent();
        });

        group.MapGet("/me", (HttpContext ctx) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();
            return Results.Ok(new MeResponse(
                ctx.User.FindFirstValue(ClaimTypes.Name),
                ctx.User.FindFirstValue(ClaimTypes.Email)));
        });

        return app;
    }

    private static string MapProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "microsoft" => "Microsoft",
        "google" => "Google",
        _ => throw new BadHttpRequestException($"Unknown provider '{provider}'."),
    };

    public sealed record MeResponse(string? DisplayName, string? Email);
}
```

- [ ] **Step 6: Wire `Program.cs`** — add auth before authorization, secure the API, map endpoints:
```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddStoreItAuthentication(builder.Configuration);   // NEW
// ... existing ProblemDetails / health / OpenApi ...

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();   // NEW
app.UseAuthorization();    // NEW
app.MapHealthChecks("/health");     // stays anonymous
app.MapOpenApi();
app.MapAuthEndpoints();              // NEW — anonymous
app.MapStorageEndpointsV1();         // now RequireAuthorization (Task 10)
await app.RunAsync();
```

- [ ] **Step 7: Add non-secret config shape** to `appsettings.json` (secrets come from env, e.g. `Authentication__Microsoft__ClientSecret`):
```json
"Authentication": {
  "Microsoft": { "Authority": "", "ClientId": "", "CallbackPath": "/auth/callback/microsoft" },
  "Google": { "Authority": "https://accounts.google.com", "ClientId": "", "CallbackPath": "/auth/callback/google" }
}
```

- [ ] **Step 8: Write failing tests** `AuthEndpointsTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using Xunit;

public class AuthEndpointsTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    [Fact]
    public async Task Me_WhenAnonymous_Returns401() // AC-05
    {
        var res = await factory.CreateClient().GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_WhenAuthenticated_ReturnsProfile() // AC-05, AC-03
    {
        var client = factory.CreateClientAs(subject: "sub-42", email: "a@x.com", name: "Alex");
        var res = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var me = await res.Content.ReadFromJsonAsync<AuthEndpoints.MeResponse>();
        Assert.Equal("Alex", me!.DisplayName);
        Assert.Equal("a@x.com", me.Email);
    }

    [Fact]
    public async Task Health_IsAnonymous() // AC-07
    {
        var res = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
```

- [ ] **Step 9: Run tests to verify they pass**
Run: `dotnet test backend/tests/StoreIt.Api.Service.Tests --filter FullyQualifiedName~AuthEndpointsTests`
Expected: PASS (Task 7 test project now compiles — `ProvisioningClaimsTransformation` exists).

- [ ] **Step 10: Commit**
```bash
git add backend/src/StoreIt.Api backend/Directory.Packages.props backend/tests/StoreIt.Api.Service.Tests/AuthEndpointsTests.cs
git commit -m "feat(backend): add BFF auth (cookie + OIDC), provisioning and auth endpoints"
```

---

### Task 9: EF migration (users + storage ownership) — Approval-tier

**Files:**
- Create: `backend/src/StoreIt.Infrastructure/Migrations/<timestamp>_AddUsersAndStorageOwnership.cs` (+ Designer + snapshot)

**Interfaces:** none (schema only).

> **Permission tier: Approval.** Running `dotnet ef` and changing the schema needs human sign-off. Fresh start: dev DBs hold only synthetic data, so pre-existing owner-less storages are removed in the migration.

- [ ] **Step 1: Generate the migration**
Run: `dotnet ef migrations add AddUsersAndStorageOwnership --project backend/src/StoreIt.Infrastructure --startup-project backend/src/StoreIt.Api`
Expected: migration + snapshot created.

- [ ] **Step 2: Make the fresh-start explicit** — edit the generated `Up(...)` so the non-nullable `OwnerId` + FK can be added on a non-empty dev DB. Add **before** the `AddColumn`/`CreateTable` for ownership:
```csharp
migrationBuilder.Sql("DELETE FROM storages;"); // cascades to items; synthetic dev data only (SPEC-003)
```
Verify the migration also creates `users` and the unique index on `(Issuer, Subject)` and the FK `storages.OwnerId → users.Id` with `ON DELETE CASCADE`.

- [ ] **Step 3: Apply and verify against a local DB**
Run: `dotnet ef database update --project backend/src/StoreIt.Infrastructure --startup-project backend/src/StoreIt.Api`
Expected: applies cleanly. (Service tests also run it via `Database.MigrateAsync()` in the fixture.)

- [ ] **Step 4: Run the service suite** to prove migrations + schema are consistent
Run: `dotnet test backend/tests/StoreIt.Api.Service.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add backend/src/StoreIt.Infrastructure/Migrations
git commit -m "feat(backend): add migration for users and storage ownership"
```

---

### Task 10: Secure endpoints, stamp OwnerId & prove isolation

**Files:**
- Modify: `backend/src/StoreIt.Application/StorageUseCases.cs` (`CreateStorageUseCase`)
- Modify: `backend/src/StoreIt.Application/ApplicationServiceCollectionExtensions.cs` (no change if already scoped)
- Modify: `backend/src/StoreIt.Api/StorageEndpoints.cs` (`RequireAuthorization` + `ProducesProblem(401)`)
- Test: `backend/tests/StoreIt.Api.Service.Tests/OwnershipTests.cs`

**Interfaces:**
- Consumes: `ICurrentUser` (Task 3), test client helper (Task 7), query filter (Task 6).

- [ ] **Step 1: Stamp `OwnerId` on create** — inject `ICurrentUser` into `CreateStorageUseCase`:
```csharp
public sealed class CreateStorageUseCase(IStorageRepository repository, ICurrentUser currentUser)
{
    public async Task<StorageSummary> ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId
            ?? throw new InvalidOperationException("No authenticated user in scope.");
        var storage = Storage.Create(name, ownerId);
        repository.Add(storage);
        await repository.SaveChangesAsync(cancellationToken);
        // ... existing summary construction ...
    }
}
```

- [ ] **Step 2: Secure the endpoints** in `StorageEndpoints.cs` — on the storages group (and the items sub-group inherits it):
```csharp
var storages = app.MapGroup("/api/v1/storages")
    .RequireAuthorization()
    .ProducesProblem(StatusCodes.Status401Unauthorized);
```

- [ ] **Step 3: Write failing ownership tests** `OwnershipTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using Xunit;

public class OwnershipTests(ApiTestFixture factory) : IClassFixture<ApiTestFixture>
{
    [Fact]
    public async Task Storages_WhenAnonymous_Returns401() // AC-01
    {
        var res = await factory.CreateClient().GetAsync("/api/v1/storages");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnStorages() // AC-08, AC-09
    {
        var alex = factory.CreateClientAs("alex");
        var blake = factory.CreateClientAs("blake");
        await alex.PostAsJsonAsync("/api/v1/storages", new { name = "Alex Pantry" });

        var blakeList = await (await blake.GetAsync("/api/v1/storages"))
            .Content.ReadFromJsonAsync<List<StorageResponse>>();
        Assert.Empty(blakeList!);

        var alexList = await (await alex.GetAsync("/api/v1/storages"))
            .Content.ReadFromJsonAsync<List<StorageResponse>>();
        Assert.Single(alexList!);
    }

    [Fact]
    public async Task GetById_OfAnotherUsersStorage_Returns404() // AC-10
    {
        var alex = factory.CreateClientAs("alex");
        var blake = factory.CreateClientAs("blake");
        var created = await (await alex.PostAsJsonAsync("/api/v1/storages", new { name = "Secret" }))
            .Content.ReadFromJsonAsync<StorageResponse>();

        var res = await blake.PutAsJsonAsync($"/api/v1/storages/{created!.Id}", new { name = "Hijack" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Items_OfAnotherUsersStorage_Returns404() // AC-11
    {
        var alex = factory.CreateClientAs("alex");
        var blake = factory.CreateClientAs("blake");
        var created = await (await alex.PostAsJsonAsync("/api/v1/storages", new { name = "Fridge" }))
            .Content.ReadFromJsonAsync<StorageResponse>();

        var res = await blake.GetAsync($"/api/v1/storages/{created!.Id}/items");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
```

- [ ] **Step 4: Run tests**
Run: `dotnet test backend/tests/StoreIt.Api.Service.Tests`
Expected: PASS — including the existing SPEC-001 endpoint tests, now run under an authenticated client (AC-12). Update any SPEC-001 service test that used a bare `CreateClient()` to use `CreateClientAs("owner")` so it operates as a signed-in owner.

- [ ] **Step 5: Run the architecture tests** (ensure no layering regressions from the new types)
Run: `dotnet test backend/tests/StoreIt.Architecture.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**
```bash
git add backend/src/StoreIt.Application backend/src/StoreIt.Api/StorageEndpoints.cs backend/tests/StoreIt.Api.Service.Tests
git commit -m "feat(backend): require auth on storage API and enforce per-user isolation"
```

---

### Task 11: Regenerate & commit the OpenAPI contract (ADR-006)

**Files:**
- Modify: `backend/openapi/StoreIt.Api.json`
- Modify: `backend/src/StoreIt.Api/AuthEndpoints.cs` (add `Produces` metadata so `/auth/*` appear in the doc)

**Interfaces:** none.

- [ ] **Step 1: Annotate auth endpoints** so they surface in the contract, e.g. on `/auth/me`:
```csharp
group.MapGet("/me", /* ... */)
     .Produces<AuthEndpoints.MeResponse>(StatusCodes.Status200OK)
     .Produces(StatusCodes.Status401Unauthorized);
```

- [ ] **Step 2: Build to regenerate the document**
Run: `dotnet build backend/src/StoreIt.Api`
Expected: `backend/openapi/StoreIt.Api.json` now includes `/auth/*` and `401` responses on `/api/v1/**`.

- [ ] **Step 3: Verify no unexpected drift** — review the diff; confirm only auth-related additions (new paths, 401 responses), no accidental changes to SPEC-001 shapes.
Run: `git diff -- backend/openapi/StoreIt.Api.json`

- [ ] **Step 4: Commit**
```bash
git add backend/openapi/StoreIt.Api.json backend/src/StoreIt.Api/AuthEndpoints.cs
git commit -m "docs(backend): update OpenAPI contract for auth and 401 responses"
```

---

### Task 12: Update threat-model R-06

**Files:**
- Modify: `docs/security/threat-model.md`

- [ ] **Step 1: Edit the R-06 row** — change status from "Not yet addressed" to mitigated, referencing ADR-004 + SPEC-003. Example replacement for the mitigation cell:
> "Mitigated: OIDC federation + BFF HttpOnly session (no tokens in browser); per-storage ownership enforced by an EF global query filter; unauthenticated `/api/v1/**` → 401, cross-user by id → 404 (ADR-004, SPEC-003)." — set the status marker to 🟢/addressed per the file's legend.
Also update the "Auth & fine-grained authorization — deferred to ADR-004" line to note it is now implemented for single-owner storages (sharing still deferred).

- [ ] **Step 2: Commit**
```bash
git add docs/security/threat-model.md
git commit -m "docs(security): mark R-06 mitigated by ADR-004/SPEC-003"
```

---

# PHASE 2 — Frontend

### Task 13: `AuthService`

**Files:**
- Create: `frontend/src/app/core/auth.service.ts`
- Modify: `frontend/proxy.conf.json` (proxy `/auth`)
- Test: `frontend/src/app/core/auth.service.spec.ts`

**Interfaces:**
- Produces: `AuthService` with `readonly user = signal<AuthUser | null | undefined>(undefined)` (undefined = not yet loaded), `loadMe(): Promise<void>`, `login(provider: 'microsoft' | 'google'): void`, `logout(): void`; `interface AuthUser { displayName: string | null; email: string | null; }`.

- [ ] **Step 1: Proxy `/auth`** — add to `frontend/proxy.conf.json`:
```json
{
  "/api": { "target": "http://localhost:5000", "secure": false },
  "/auth": { "target": "http://localhost:5000", "secure": false }
}
```

- [ ] **Step 2: Write the failing test** `auth.service.spec.ts`
```typescript
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sets the user on a 200 from /auth/me', async () => {
    const done = service.loadMe();
    http.expectOne('/auth/me').flush({ displayName: 'Alex', email: 'a@x.com' });
    await done;
    expect(service.user()).toEqual({ displayName: 'Alex', email: 'a@x.com' });
  });

  it('sets null when /auth/me returns 401', async () => {
    const done = service.loadMe();
    http.expectOne('/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await done;
    expect(service.user()).toBeNull();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**
Run: `cd frontend && npx vitest run src/app/core/auth.service.spec.ts`
Expected: FAIL — `AuthService` does not exist.

- [ ] **Step 4: Write minimal implementation** `auth.service.ts`
```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface AuthUser {
  displayName: string | null;
  email: string | null;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  readonly user = signal<AuthUser | null | undefined>(undefined);

  async loadMe(): Promise<void> {
    try {
      const me = await firstValueFrom(this.http.get<AuthUser>('/auth/me'));
      this.user.set(me);
    } catch {
      this.user.set(null);
    }
  }

  login(provider: 'microsoft' | 'google'): void {
    const returnUrl = encodeURIComponent(window.location.pathname);
    window.location.assign(`/auth/login/${provider}?returnUrl=${returnUrl}`);
  }

  logout(): void {
    firstValueFrom(this.http.post('/auth/logout', {})).finally(() => {
      this.user.set(null);
      window.location.assign('/login');
    });
  }
}
```

- [ ] **Step 5: Run tests to verify they pass**
Run: `cd frontend && npx vitest run src/app/core/auth.service.spec.ts`
Expected: PASS.

- [ ] **Step 6: Commit**
```bash
git add frontend/src/app/core/auth.service.ts frontend/src/app/core/auth.service.spec.ts frontend/proxy.conf.json
git commit -m "feat(frontend): add AuthService with session signal and provider login"
```

---

### Task 14: 401 interceptor

**Files:**
- Create: `frontend/src/app/core/auth.interceptor.ts`
- Modify: `frontend/src/app/app.config.ts` (`withInterceptors`)
- Test: `frontend/src/app/core/auth.interceptor.spec.ts`

**Interfaces:**
- Produces: `authInterceptor: HttpInterceptorFn` — on a 401 from any URL except `/auth/me`, clears the session signal and navigates to `/login`.
- Consumes: `AuthService` (Task 13).

- [ ] **Step 1: Write the failing test** `auth.interceptor.spec.ts`
```typescript
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  it('redirects to /login and clears session on 401', () => {
    const navigate = vi.spyOn(router, 'navigateByUrl');
    http.get('/api/v1/storages').subscribe({ error: () => {} });
    ctrl.expectOne('/api/v1/storages').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(TestBed.inject(AuthService).user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('does not redirect on a 401 from /auth/me', () => {
    const navigate = vi.spyOn(router, 'navigateByUrl');
    http.get('/auth/me').subscribe({ error: () => {} });
    ctrl.expectOne('/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(navigate).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**
Run: `cd frontend && npx vitest run src/app/core/auth.interceptor.spec.ts`
Expected: FAIL — `authInterceptor` does not exist.

- [ ] **Step 3: Write minimal implementation** `auth.interceptor.ts`
```typescript
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !req.url.endsWith('/auth/me')) {
        auth.user.set(null);
        router.navigateByUrl('/login');
      }
      return throwError(() => error);
    }),
  );
};
```

- [ ] **Step 4: Wire it** in `app.config.ts`:
```typescript
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './core/auth.interceptor';
// ...
provideHttpClient(withInterceptors([authInterceptor])),
```

- [ ] **Step 5: Run tests to verify they pass**
Run: `cd frontend && npx vitest run src/app/core/auth.interceptor.spec.ts`
Expected: PASS.

- [ ] **Step 6: Commit**
```bash
git add frontend/src/app/core/auth.interceptor.ts frontend/src/app/core/auth.interceptor.spec.ts frontend/src/app/app.config.ts
git commit -m "feat(frontend): redirect to login on 401 via interceptor"
```

---

### Task 15: Auth guard, `/login` route & login page

**Files:**
- Create: `frontend/src/app/core/auth.guard.ts`
- Create: `frontend/src/app/auth/login-page.ts` (+ `login-page.html`)
- Modify: `frontend/src/app/app.routes.ts`
- Test: `frontend/src/app/core/auth.guard.spec.ts`

**Interfaces:**
- Produces: `authGuard: CanActivateFn` — ensures the session is loaded (`loadMe()` if `undefined`); returns `true` when authenticated, otherwise a `UrlTree` to `/login`.
- Consumes: `AuthService` (Task 13).

- [ ] **Step 1: Write the failing guard test** `auth.guard.spec.ts`
```typescript
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { runInInjectionContext } from '@angular/core';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  it('allows navigation when a user is present', async () => {
    TestBed.inject(AuthService).user.set({ displayName: 'A', email: null });
    const result = await runInInjectionContext(TestBed.injector, () =>
      authGuard({} as any, {} as any));
    expect(result).toBe(true);
  });

  it('redirects to /login when unauthenticated', async () => {
    TestBed.inject(AuthService).user.set(null);
    const result = await runInInjectionContext(TestBed.injector, () =>
      authGuard({} as any, {} as any));
    expect((result as any).toString()).toContain('/login');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**
Run: `cd frontend && npx vitest run src/app/core/auth.guard.spec.ts`
Expected: FAIL — `authGuard` does not exist.

- [ ] **Step 3: Write the guard** `auth.guard.ts`
```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.user() === undefined) await auth.loadMe();
  return auth.user() ? true : router.parseUrl('/login');
};
```

- [ ] **Step 4: Write the login page** `login-page.ts`
```typescript
import { Component, inject } from '@angular/core';
import { TranslatePipe } from '../core/translate';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-login-page',
  imports: [TranslatePipe],
  templateUrl: './login-page.html',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  protected login(provider: 'microsoft' | 'google'): void {
    this.auth.login(provider);
  }
}
```
`login-page.html`
```html
<section class="login">
  <h1>{{ 'auth.login.title' | translate }}</h1>
  <p>{{ 'auth.login.subtitle' | translate }}</p>
  <button type="button" class="btn-provider" (click)="login('microsoft')">
    {{ 'auth.login.microsoft' | translate }}
  </button>
  <button type="button" class="btn-provider" (click)="login('google')">
    {{ 'auth.login.google' | translate }}
  </button>
</section>
```

- [ ] **Step 5: Wire routes** `app.routes.ts`
```typescript
import { authGuard } from './core/auth.guard';
import { LoginPage } from './auth/login-page';

export const routes: Routes = [
  { path: 'login', component: LoginPage },
  { path: 'storages', component: StorageListPage, canActivate: [authGuard] },
  { path: 'storages/:id', component: StorageDetailPage, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'storages' },
  { path: '**', redirectTo: 'storages' },
];
```

- [ ] **Step 6: Run tests**
Run: `cd frontend && npx vitest run src/app/core/auth.guard.spec.ts`
Expected: PASS.

- [ ] **Step 7: Commit**
```bash
git add frontend/src/app/core/auth.guard.ts frontend/src/app/core/auth.guard.spec.ts frontend/src/app/auth frontend/src/app/app.routes.ts
git commit -m "feat(frontend): protect routes with auth guard and add login page"
```

---

### Task 16: Session header + logout

**Files:**
- Modify: `frontend/src/app/app.ts`, `frontend/src/app/app.html`
- Test: `frontend/src/app/app.spec.ts` (create if absent)

**Interfaces:**
- Consumes: `AuthService` (Task 13).

- [ ] **Step 1: Write the failing test** `app.spec.ts`
```typescript
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { AuthService } from './core/auth.service';
import { TranslateService } from './core/translate';

describe('App header', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('en', {
      auth: { session: { signedInAs: 'Signed in as {{name}}', logout: 'Log out' } },
      nav: { storages: 'Storages' }, header: { language: 'Language' }, languages: {},
    });
  });

  it('shows the display name and a logout button when signed in', () => {
    TestBed.inject(AuthService).user.set({ displayName: 'Alex', email: 'a@x.com' });
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Alex');
    expect(fixture.nativeElement.querySelector('.logout')).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**
Run: `cd frontend && npx vitest run src/app/app.spec.ts`
Expected: FAIL — no session UI / no `.logout`.

- [ ] **Step 3: Implement** — inject the service in `app.ts`:
```typescript
protected readonly auth = inject(AuthService);
constructor() {
  this.language.init();
  void this.auth.loadMe();
}
protected logout(): void { this.auth.logout(); }
```
Add to `app.html` inside `.header-right` (after the language `<select>`):
```html
@if (auth.user(); as user) {
  <span class="session">{{ 'auth.session.signedInAs' | translate: { name: user.displayName ?? user.email } }}</span>
  <button type="button" class="logout" (click)="logout()">{{ 'auth.session.logout' | translate }}</button>
}
```

- [ ] **Step 4: Run tests**
Run: `cd frontend && npx vitest run src/app/app.spec.ts`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add frontend/src/app/app.ts frontend/src/app/app.html frontend/src/app/app.spec.ts
git commit -m "feat(frontend): show signed-in user and logout in header"
```

---

### Task 17: i18n strings (de/en/fr/it)

**Files:**
- Modify: `frontend/public/assets/i18n/en.json`, `de.json`, `fr.json`, `it.json`

**Interfaces:** none. The existing `i18n.spec.ts` guard fails unless **all four** files have identical key sets — so add the same keys everywhere.

- [ ] **Step 1: Add the keys to `en.json`** (merge into existing structure)
```json
"auth": {
  "login": {
    "title": "Sign in to store-it",
    "subtitle": "Use your existing account.",
    "microsoft": "Sign in with Microsoft",
    "google": "Sign in with Google"
  },
  "session": {
    "signedInAs": "Signed in as {{name}}",
    "logout": "Log out"
  }
},
"errors": { "auth": { "sessionExpired": "Your session has expired. Please sign in again." } }
```

- [ ] **Step 2: Add the same keys to `de.json`**
```json
"auth": {
  "login": { "title": "Bei store-it anmelden", "subtitle": "Verwende dein bestehendes Konto.", "microsoft": "Mit Microsoft anmelden", "google": "Mit Google anmelden" },
  "session": { "signedInAs": "Angemeldet als {{name}}", "logout": "Abmelden" }
},
"errors": { "auth": { "sessionExpired": "Deine Sitzung ist abgelaufen. Bitte melde dich erneut an." } }
```

- [ ] **Step 3: Add the same keys to `fr.json`**
```json
"auth": {
  "login": { "title": "Se connecter à store-it", "subtitle": "Utilisez votre compte existant.", "microsoft": "Se connecter avec Microsoft", "google": "Se connecter avec Google" },
  "session": { "signedInAs": "Connecté en tant que {{name}}", "logout": "Se déconnecter" }
},
"errors": { "auth": { "sessionExpired": "Votre session a expiré. Veuillez vous reconnecter." } }
```

- [ ] **Step 4: Add the same keys to `it.json`**
```json
"auth": {
  "login": { "title": "Accedi a store-it", "subtitle": "Usa il tuo account esistente.", "microsoft": "Accedi con Microsoft", "google": "Accedi con Google" },
  "session": { "signedInAs": "Connesso come {{name}}", "logout": "Esci" }
},
"errors": { "auth": { "sessionExpired": "La tua sessione è scaduta. Effettua di nuovo l'accesso." } }
```

- [ ] **Step 5: Run the i18n guard + full unit suite**
Run: `cd frontend && npx vitest run`
Expected: PASS (`i18n.spec.ts` confirms all four files share the same keys).

- [ ] **Step 6: Commit**
```bash
git add frontend/public/assets/i18n
git commit -m "feat(frontend): add auth i18n strings for de/en/fr/it"
```

---

### Task 18: E2E — logged-in flow via a dev-only login hook

**Files:**
- Create: `backend/src/StoreIt.Api/DevAuthEndpoints.cs` (mapped ONLY in Development)
- Modify: `backend/src/StoreIt.Api/Program.cs` (conditional mapping)
- Create: `frontend/e2e/auth.spec.ts`

**Interfaces:**
- Produces: `POST /auth/dev-login` (Development-only) that signs a cookie for a fixed synthetic user, so Playwright can establish a session without a real IdP.

> **Security:** this endpoint is mapped only when `app.Environment.IsDevelopment()`. It must never be reachable in staging/production. Call this out explicitly in the PR description for the reviewer (Gate G2).

- [ ] **Step 1: Add the dev-only endpoint** `DevAuthEndpoints.cs`
```csharp
namespace StoreIt.Api;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

public static class DevAuthEndpoints
{
    public static IEndpointRouteBuilder MapDevAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/dev-login", async (HttpContext ctx) =>
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "e2e-user"),
                new Claim("iss", "dev"),
                new Claim(ClaimTypes.Name, "E2E User"),
                new Claim(ClaimTypes.Email, "e2e@example.com"),
            ], CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return TypedResults.NoContent();
        });
        return app;
    }
}
```

- [ ] **Step 2: Map it only in Development** in `Program.cs`
```csharp
if (app.Environment.IsDevelopment())
    app.MapDevAuthEndpoints();
```

- [ ] **Step 3: Write the E2E spec** `frontend/e2e/auth.spec.ts`
```typescript
import { expect, test } from '@playwright/test';

test('unauthenticated visit to /storages redirects to the login screen', async ({ page }) => {
  await page.goto('/storages');
  await expect(page.getByRole('button', { name: /microsoft/i })).toBeVisible();
});

test('after dev-login the storages overview is reachable', async ({ page, request }) => {
  await request.post('/auth/dev-login');            // establishes the session cookie
  await page.goto('/storages');
  await expect(page.getByRole('button', { name: /neuer schrank|new storage/i })).toBeVisible();
});
```

- [ ] **Step 4: Run E2E** (backend in Development + frontend dev server per `playwright.config.ts`)
Run: `cd frontend && npx playwright test auth.spec.ts`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add backend/src/StoreIt.Api/DevAuthEndpoints.cs backend/src/StoreIt.Api/Program.cs frontend/e2e/auth.spec.ts
git commit -m "test(e2e): cover login redirect and authenticated overview via dev-login"
```

---

## Final verification (before requesting review)

- [ ] Backend: `dotnet test backend/StoreIt.sln` — all suites green (domain, service, architecture).
- [ ] Backend: `dotnet build backend/StoreIt.sln` — zero warnings (warnings-as-errors).
- [ ] Frontend: `cd frontend && npx vitest run` — green, coverage ≥ 70%.
- [ ] Frontend: `cd frontend && npx playwright test` — green.
- [ ] Contract: `git diff --stat -- backend/openapi/StoreIt.Api.json` shows only intended auth changes.
- [ ] Update the SPEC-003 Verification table (fill in the real test names) and flip its Gate G2 row once the AI + human review pass.
- [ ] Write the agent run log under `docs/agent-logs/` (DoD requirement).
- [ ] Hand off: the human pushes the implementation branch `feature/spec-003-auth-impl`, opens the PR to `develop`, and runs Gates G2/G3. **Highlight the Development-only `/auth/dev-login` endpoint in the PR description.** (The baseline docs PR on `feature/spec-003-auth` is merged first, before this implementation branch is cut.)
