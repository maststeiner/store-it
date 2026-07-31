# Accounts & Storage Ownership — Implementation Plan (SPEC-003 / ADR-004)

> **For agentic workers:** execute task-by-task with superpowers:subagent-driven-development or superpowers:executing-plans. Each task is TDD (**write the failing test first, then the minimal code, then commit**) and ends with an independently reviewable deliverable. Checkboxes (`- [ ]`) track progress.
>
> **Plan style:** lean and architect-reviewable — Files + Interfaces (signatures) + intent + test names. Code appears **only where it is non-obvious or security-critical**; everything else follows from the signatures and the existing codebase patterns.

**Goal:** Federated login (Microsoft + Google via direct OIDC, BFF session) with per-storage ownership, so each user sees only the storages/items they created.

**Architecture:** The .NET API is a Backend-for-Frontend — OpenID Connect handlers do the code+PKCE exchange server-side and issue an HttpOnly cookie; no tokens in the browser. A local `User` is provisioned once at the OIDC callback, keyed by `(Issuer, Subject)`. Ownership is enforced centrally by an EF Core global query filter on `Storage` (cross-user reads match nothing → 404). Angular gains a login screen, route guard, 401 interceptor and session header.

**Tech Stack:** .NET 10 (minimal APIs, EF Core 10 + Npgsql, cookie + OpenIdConnect), Angular 22 (standalone, signals), xUnit + Testcontainers.PostgreSql + `WebApplicationFactory`, Vitest + Playwright.

## Global Constraints

- **Layering (ADR-001):** Domain → (none); Application → Domain; Infrastructure → Application+Domain; Api → Application+Domain (only `Program.cs` touches Infrastructure). Ownership/authorization logic lives in Application/Infrastructure — never in Api handlers or the frontend. Enforced by `StoreIt.Architecture.Tests`.
- **API-first + contract gate (ADR-002/006):** endpoints stay under `/api/v1/**`; the canonical OpenAPI artifact `backend/openapi/StoreIt.Api.json` is generated on build and committed (reviewed diff). Path stays `v1` (requiring auth is a deliberate behavioural change, no `v2`).
- **Strict build:** `net10.0`, `Nullable=enable`, `CodeAnalysisTreatWarningsAsErrors=true`.
- **Central Package Management:** versions in `backend/Directory.Packages.props`.
- **Persistence (ADR-003):** PostgreSQL + EF Core; domain-generated GUID keys; entity config via `IEntityTypeConfiguration`.
- **Secrets:** per-provider OIDC client id/secret from the environment (12-factor); never committed.
- **Authorization semantics:** unauthenticated on `/api/v1/**` → **401**; cross-user access by id → **404**; `/health` open, no IdP dependency.
- **Frontend:** standalone + signals; in-house `TranslateService`/`translate` pipe (no new i18n lib); all user-facing strings in de/en/fr/it; functional guards/interceptors.
- **Branch/PR:** baseline docs PR (SPEC-003 + ADR-004 + this plan) merges to `develop` first; implementation then on `feature/spec-003-auth-impl` off `develop`. Commit locally; the human pushes/opens PRs/merges. DB/schema-migration commands are Approval-tier.

## AC → Task coverage

| AC / EC | Task | AC / EC | Task |
|---|---|---|---|
| AC-01 401 unauth | 8,10 | AC-08 owner set server-side | 2,10 |
| AC-02 login → session | 8 | AC-09 list own only | 6,10 |
| AC-03 JIT provision | 4,8 | AC-10 cross-user → 404 | 6,10 |
| AC-04 known user reused | 4 | AC-11 items scoped | 6,10 |
| AC-05 `/auth/me` | 8 | AC-12 SPEC-001 unchanged | 10 |
| AC-06 logout | 8 | EC-01 expired→login | 14 |
| AC-07 `/health` open | 10 | EC-02 no-email fallback | 1,4 |
| EC-03 two providers→two users | 4 | EC-04 invalid code/state | 8 (framework) |
| Repo-level isolation | 6 | CSRF on mutations | 8a |

## File Structure

**Backend — new:** `Domain/User.cs` · `Application/ICurrentUser.cs` · `Application/IUserRepository.cs` · `Application/UserUseCases.cs` (`ProvisionUserUseCase` + `UserAlreadyExistsException`) · `Infrastructure/UserConfiguration.cs` · `Infrastructure/UserRepository.cs` · `Infrastructure/DesignTimeDbContextFactory.cs` · `Api/CurrentUser.cs` · `Api/AuthenticationSetup.cs` · `Api/AuthEndpoints.cs` · `tests/…Service.Tests/TestAuthHandler.cs`.

**Backend — modified:** `Domain/Storage.cs` (OwnerId) · `Infrastructure/StorageConfiguration.cs` (FK) · `Infrastructure/StoreItDbContext.cs` (`DbSet<User>`, `ICurrentUser`, query filter) · `Infrastructure/InfrastructureServiceCollectionExtensions.cs` · `Application/StorageUseCases.cs` (`CreateStorageUseCase`) · `Application/ApplicationServiceCollectionExtensions.cs` · `Api/Program.cs` · `Api/StorageEndpoints.cs` · `Directory.Packages.props` + `Api.csproj` · `Migrations/*` · `openapi/StoreIt.Api.json` · `docs/security/threat-model.md`.

**Frontend — new:** `core/auth.service.ts` · `core/auth.interceptor.ts` · `core/auth.guard.ts` · `auth/login-page.ts` (+ html).
**Frontend — modified:** `app.config.ts` · `app.routes.ts` · `app.ts`/`app.html` · `public/assets/i18n/{de,en,fr,it}.json` · `proxy.conf.json` · `e2e/auth.spec.ts` (new).

---

# PHASE 1 — Backend

### Task 1: `User` domain entity

- **Files:** create `Domain/User.cs`; test `Domain.Tests/UserTests.cs`.
- **Interface:** `User.Create(string issuer, string subject, string? email, string? displayName, DateTimeOffset createdAt) : User`; props `Id, Issuer, Subject, Email?, DisplayName, CreatedAt`; `UpdateProfile(string? email, string? displayName)`.
- **Intent:** account keyed by `(Issuer, Subject)`. Empty issuer/subject → `DomainValidationException("user.issuer.empty" / "user.subject.empty")`.
- **EC-02 fallback (non-obvious — keep):** `DisplayName` is never empty. Both `Create` and `UpdateProfile` route through:
  ```csharp
  private static string ResolveDisplayName(string? displayName, string? email, string subject) =>
      !string.IsNullOrWhiteSpace(displayName) ? displayName!
      : !string.IsNullOrWhiteSpace(email)     ? email!
      : $"user-{subject[..Math.Min(8, subject.Length)]}";
  ```
- **Tests:** `Create_SetsFieldsAndGeneratesId` · `Create_WithNoEmailOrName_UsesDisplayNameFallback` (→ `user-<sub≤8>`) · `Create_WithNoName_FallsBackToEmail` · `Create_MissingIssuerOrSubject_Throws` · `UpdateProfile_ChangesEmailAndName`.

### Task 2: `Storage` ownership (domain)

- **Files:** modify `Domain/Storage.cs`; update existing `Domain.Tests/StorageTests.cs` call sites.
- **Interface change:** `Storage.Create(string name, Guid ownerId)`; new prop `Guid OwnerId`.
- **Intent:** `ownerId == Guid.Empty` → `DomainValidationException("storage.owner.missing")`. Update all existing `Create("x")` calls to pass an owner.
- **Tests:** `Create_WithOwner_SetsOwnerId` · `Create_WithEmptyOwner_Throws`; full domain suite stays green.

### Task 3: `ICurrentUser` port

- **Files:** create `Application/ICurrentUser.cs`.
- **Interface:** `interface ICurrentUser { Guid? UserId { get; } }` (null when anonymous). No test — consumed later.

### Task 4: `IUserRepository` + `ProvisionUserUseCase`

- **Files:** create `Application/IUserRepository.cs`, `Application/UserUseCases.cs`; register in `ApplicationServiceCollectionExtensions`; test `Domain.Tests/ProvisionUserUseCaseTests.cs` (in-memory fake repo, no DB).
- **Interfaces:**
  - `IUserRepository { Task<User?> GetBySubjectAsync(issuer, subject, ct); void Add(User); Task SaveChangesAsync(ct); }`
  - `ProvisionUserUseCase.ExecuteAsync(issuer, subject, email?, displayName?, ct) : Task<User>` — find-or-create, refresh profile every login.
  - `UserAlreadyExistsException` (parameterless) — raised by the repository on a concurrent-insert unique violation.
- **Intent + race handling (:423, non-obvious — keep the shape):** existing → refresh & return. Else create+save; **on `UserAlreadyExistsException`** (a concurrent first login won) → re-`GetBySubjectAsync` the winner, refresh, return. Application stays free of EF/Npgsql types — the repository (Task 5) translates the DB error.
- **Tests:** `FirstLogin_CreatesUser` · `SecondLogin_ReusesUserAndRefreshesProfile` · `DifferentIssuers_ProduceSeparateUsers` (EC-03) · `ConcurrentFirstLogin_WhenInsertRaces_ReturnsWinner` (fake repo throws `UserAlreadyExistsException` on first save, then returns the winner).
- **Deps:** needs `Microsoft.Extensions.TimeProvider.Testing` for `FakeTimeProvider` (add to `Directory.Packages.props` + test csproj if absent).

### Task 5: User persistence (Infrastructure)

- **Files:** create `UserConfiguration.cs`, `UserRepository.cs`; add `DbSet<User>` to `StoreItDbContext`; register `IUserRepository`.
- **Intent:** table `users`, `ValueGeneratedNever` Id, **unique index on `(Issuer, Subject)`**. Repository is standard CRUD **except** `SaveChangesAsync` translates the Npgsql unique violation to the Application signal (non-obvious — keep):
  ```csharp
  catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
      { throw new UserAlreadyExistsException(); }
  ```
- **Verification:** covered end-to-end by the authenticated service tests (Task 10); no standalone DB test.

### Task 6: Ownership via EF global query filter

- **Files:** modify `StoreItDbContext.cs` (inject `ICurrentUser`, add filter), `StorageConfiguration.cs` (FK); create `DesignTimeDbContextFactory.cs`.
- **Mechanism (core of the feature — keep):**
  ```csharp
  public class StoreItDbContext(DbContextOptions<StoreItDbContext> options, ICurrentUser currentUser) : DbContext(options)
  {
      protected override void OnModelCreating(ModelBuilder mb)
      {
          mb.ApplyConfigurationsFromAssembly(typeof(StoreItDbContext).Assembly);
          mb.Entity<Storage>().HasQueryFilter(s => s.OwnerId == currentUser.UserId); // anonymous ⇒ matches nothing
      }
  }
  ```
  Items are reachable only through the `Storage` aggregate (`Include(s => s.Items)`, no `DbSet<Item>`), so the storage-level filter fully covers items (AC-11). A by-id lookup of another user's storage returns `null` → the handler's existing `StorageNotFoundException` → **404** (this is how AC-10 is met by default).
- **`StorageConfiguration`:** `OwnerId` required, FK `→ users.Id`, `OnDelete(Cascade)`, index on `OwnerId`.
- **`DesignTimeDbContextFactory`:** supplies a null-current-user context so `dotnet ef` works without a request scope.
- **Verification:** proven in Task 10 (user A vs B).

### Task 7: Test authentication handler

- **Files:** create `Service.Tests/TestAuthHandler.cs`; extend `ApiTestFixture` (register `"Test"` scheme as default, add `CreateClientAs(subject, issuer?, email?, name?)`).
- **Intent:** a scheme reading `X-Test-Subject/Issuer/Email/Name` headers → builds a principal, **provisions via `ProvisionUserUseCase` and stamps the `sub_local` claim itself** (mirrors production's `OnTokenValidated`, so tests exercise real provisioning without a live IdP). No `X-Test-Subject` → `NoResult()` (→ 401 on protected routes).
- **Note:** compiles once Task 8 provides `CurrentUser.LocalIdClaim`; Tasks 7 & 8 land together.

### Task 8: Auth wiring, provisioning & auth endpoints

- **Files:** packages (`Directory.Packages.props` + `Api.csproj`: `Microsoft.AspNetCore.Authentication.OpenIdConnect`); create `CurrentUser.cs`, `AuthenticationSetup.cs`, `AuthEndpoints.cs`; modify `Program.cs`, `appsettings.json` (non-secret shape); test `Service.Tests/AuthEndpointsTests.cs`.
- **Interfaces:** `IServiceCollection.AddStoreItAuthentication(IConfiguration)` (cookie + per-provider OIDC + `OnTokenValidated` provisioning + fallback authz policy) · `IEndpointRouteBuilder.MapAuthEndpoints()` · `CurrentUser : ICurrentUser` (reads `sub_local` from the cookie principal).
- **Endpoints (all anonymous):** `GET /auth/login/{provider}` · `GET /auth/callback/{provider}` (OIDC) · `POST /auth/logout` (clears cookie) · `GET /auth/me` (200 profile / 401).
- **Cookie:** `HttpOnly`, `SameSite=Lax`, `SecurePolicy=Always`; `OnRedirectToLogin/AccessDenied` return 401/403 instead of redirecting (it's an API).
- **Provision-once (:764 — non-obvious, keep):** provisioning runs in the OIDC callback, **not** per request — so later requests are read-only.
  ```csharp
  options.Events.OnTokenValidated = async ctx => {
      var provision = ctx.HttpContext.RequestServices.GetRequiredService<ProvisionUserUseCase>();
      var p = ctx.Principal!;
      var subject = p.FindFirstValue(ClaimTypes.NameIdentifier) ?? p.FindFirstValue("sub");
      if (subject is null) { ctx.Fail("No subject claim."); return; }
      var user = await provision.ExecuteAsync(p.FindFirstValue("iss") ?? options.Authority!, subject,
                     p.FindFirstValue(ClaimTypes.Email), p.FindFirstValue("name") ?? p.FindFirstValue(ClaimTypes.Name),
                     ctx.HttpContext.RequestAborted);
      ((ClaimsIdentity)p.Identity!).AddClaim(new Claim(CurrentUser.LocalIdClaim, user.Id.ToString()));
  };
  ```
- **Secure-by-default (:800 — keep):** `options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();` — every endpoint requires auth unless it opts out with `AllowAnonymous` (`/auth/*` group, `/health`, OpenAPI).
- **Open-redirect guard (:838 — keep):** `login` only accepts an app-local `returnUrl`:
  ```csharp
  static string Safe(string? r) => !string.IsNullOrEmpty(r) && r.StartsWith('/')
      && !r.StartsWith("//") && !r.StartsWith("/\\") ? r : "/";
  ```
- **Config:** `Authentication:{Microsoft,Google}:{Authority,ClientId,ClientSecret,CallbackPath}` — secrets via env (`Authentication__Microsoft__ClientSecret`).
- **Tests:** `Me_Anonymous_Returns401` · `Me_Authenticated_ReturnsProfile` · `Health_IsAnonymous`.

### Task 8a: CSRF protection for cookie mutations (:796)

- **Files:** antiforgery in `AuthenticationSetup`/`Program.cs`; validate on the storages group in `StorageEndpoints.cs`; frontend header in `auth.interceptor.ts`; test `Service.Tests/CsrfTests.cs`.
- **Intent:** double-submit token — `GET /auth/csrf` sets a readable `XSRF-TOKEN` cookie; state-changing `/api/v1/**` must echo `X-XSRF-TOKEN`; missing/mismatch → **403** (`SameSite=Lax` alone is insufficient). Config: `AddAntiforgery(HeaderName="X-XSRF-TOKEN", Cookie.Name="XSRF-TOKEN", HttpOnly=false)`, `app.UseAntiforgery()`.
- **Validation (non-obvious — keep):** endpoint filter on the storages group:
  ```csharp
  storages.AddEndpointFilter(async (ctx, next) => {
      var m = ctx.HttpContext.Request.Method;
      if (HttpMethods.IsPost(m) || HttpMethods.IsPut(m) || HttpMethods.IsDelete(m)) {
          var af = ctx.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
          try { await af.ValidateRequestAsync(ctx.HttpContext); }
          catch (AntiforgeryValidationException) { return Results.StatusCode(403); }
      }
      return await next(ctx);
  });
  ```
- **Tests:** `Post_WithoutCsrfToken_Returns403`. Extend `CreateClientAs` with a CSRF-priming helper so the authenticated POST/PUT/DELETE tests (Tasks 8/10) keep passing.

### Task 9: EF migration — Approval-tier

- **Files:** `Migrations/<ts>_AddUsersAndStorageOwnership.*`.
- **Intent:** creates `users` + unique `(Issuer, Subject)`; adds `Storage.OwnerId` **NOT NULL, no server default**; FK `→ users.Id` `ON DELETE CASCADE`.
- **Non-destructive (:959 — keep the rule):** **no blanket `DELETE FROM storages`** — migrations may run against staging/prod. On an empty dev table the NOT-NULL-no-default add succeeds; on a populated table it **fails loudly** → recreate the dev DB (`database drop` + `update`). Guard comment at the top of `Up(...)` stating this.
- **Verify:** service suite runs the migration via `Database.MigrateAsync()` in the fixture.

### Task 10: Secure endpoints, stamp OwnerId & prove isolation

- **Files:** modify `CreateStorageUseCase` (inject `ICurrentUser`, `Storage.Create(name, currentUser.UserId!.Value)`); `StorageEndpoints.cs` (`MapGroup("/api/v1/storages").RequireAuthorization().ProducesProblem(401)`); test `Service.Tests/OwnershipTests.cs`.
- **Intent:** create stamps owner server-side; all reads are already scoped by the query filter, so cross-user by-id naturally 404s. Update any SPEC-001 service test using a bare `CreateClient()` to `CreateClientAs("owner")` (AC-12).
- **Tests:** `Storages_Anonymous_Returns401` · `List_ReturnsOnlyOwnStorages` · `GetById_OtherUsersStorage_Returns404` · `Items_CrossUser_AllOperationsReturn404` (**read + create + update + delete**, :1064). Then run `Architecture.Tests` (no layering regressions).

### Task 11: Regenerate & commit the OpenAPI contract

- **Files:** `openapi/StoreIt.Api.json`; add `Produces`/`ProducesProblem` metadata on `/auth/*` and `401` on `/api/v1/**`.
- **Intent:** build regenerates the doc; review the diff — only auth additions (new paths, 401s), no SPEC-001 shape changes.

### Task 12: Update threat-model R-06

- **Files:** `docs/security/threat-model.md`.
- **Intent:** flip R-06 from "Not yet addressed" to **mitigated** (ref ADR-004 + SPEC-003) — **only now**, with the implementation tests green (do not claim it earlier). Note fine-grained sharing still deferred.

---

# PHASE 2 — Frontend

### Task 13: `AuthService`

- **Files:** create `core/auth.service.ts`; add `/auth` to `proxy.conf.json`; on app init `GET /auth/csrf` once (Task 8a); test `auth.service.spec.ts`.
- **Interface:** `user = signal<AuthUser|null|undefined>(undefined)` (undefined = unknown) · `loadError = signal(false)` · `loadMe(): Promise<void>` · `login('microsoft'|'google')` (redirect) · `logout(): Promise<void>`. `AuthUser { displayName: string|null; email: string|null }`.
- **Intent (:1214/:1226 — keep the rules):** `loadMe` sets `user=null` **only on 401**; other failures set `loadError=true` (a network/5xx must not turn a signed-in user anonymous). `logout` `await`s the POST in `try/catch/finally`, then clears state and redirects — no unhandled rejection.
- **Tests:** `loadMe_sets_user_on_200` · `loadMe_sets_null_on_401` · `loadMe_sets_loadError_on_500` · `logout_clears_and_redirects_even_on_error`.

### Task 14: 401 interceptor + CSRF header

- **Files:** create `core/auth.interceptor.ts`; wire `provideHttpClient(withInterceptors([authInterceptor]))` in `app.config.ts`; test `auth.interceptor.spec.ts`.
- **Intent:** on `401` from any URL **except `/auth/me`** → clear session signal + `router.navigateByUrl('/login')` (EC-01). On mutating requests attach `X-XSRF-TOKEN` from the `XSRF-TOKEN` cookie (Task 8a).
- **Tests:** `redirects_and_clears_on_401` · `ignores_401_from_auth_me` · `attaches_xsrf_header_on_post`.

### Task 15: Guard, `/login` route & login page

- **Files:** create `core/auth.guard.ts`, `auth/login-page.ts` (+ html); modify `app.routes.ts`.
- **Interface:** `authGuard: CanActivateFn` — if `user()===undefined` `await loadMe()`; return `true` or `router.parseUrl('/login')`.
- **Intent:** protect `storages` + `storages/:id` with `canActivate:[authGuard]`; add public `login` route. Login page: minimal, two buttons → `auth.login('microsoft'|'google')`, `translate` pipe, no password form.
- **Tests (:1384 — provide `HttpClient` in the test):** `allows_when_user_present` · `redirects_to_login_when_anonymous`.

### Task 16: Session header + logout

- **Files:** modify `app.ts` (inject `AuthService`, `loadMe()` on init, `logout()`), `app.html` (in `.header-right`: `@if (auth.user(); as u)` → signed-in-as + logout button); test `app.spec.ts`.
- **Tests:** `shows_display_name_and_logout_when_signed_in`.

### Task 17: i18n strings (de/en/fr/it)

- **Files:** `public/assets/i18n/{de,en,fr,it}.json`.
- **Intent:** add the **same keys to all four** files (the existing `i18n.spec.ts` enforces identical key sets): `auth.login.{title,subtitle,microsoft,google}`, `auth.session.{signedInAs,logout}`, `errors.auth.sessionExpired`.
- **Verify:** `vitest run` (i18n guard passes).

### Task 18: E2E — login redirect + authenticated overview

- **Files:** create dev-only `Api/DevAuthEndpoints.cs` (`POST /auth/dev-login`, mapped **only** when `app.Environment.IsDevelopment()`); map conditionally in `Program.cs`; create `e2e/auth.spec.ts`.
- **Security:** dev-login is never reachable outside Development — call this out in the PR description (G2).
- **Tests:** `unauthenticated_/storages_redirects_to_login` · `after_dev_login_overview_is_reachable` — use **`page.request.post('/auth/dev-login')`** (:1659) so the session cookie lands in the browser context `page.goto` uses.

---

## Final verification (before requesting review)

- [ ] `dotnet build backend/StoreIt.sln` — zero warnings (warnings-as-errors).
- [ ] `dotnet test backend/StoreIt.sln` — domain, service, architecture green.
- [ ] `cd frontend && npx vitest run` — green, coverage ≥ 70%.
- [ ] `cd frontend && npx playwright test` — green.
- [ ] `git diff --stat -- backend/openapi/StoreIt.Api.json` — only intended auth changes.
- [ ] Fill the SPEC-003 Verification table with real test names; R-06 flipped only after green.
- [ ] Agent run log written; PR description highlights the Development-only `/auth/dev-login`.
- [ ] Human pushes `feature/spec-003-auth-impl`, opens the PR to `develop`, runs Gates G2/G3.
