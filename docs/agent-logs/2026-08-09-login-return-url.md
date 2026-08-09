# Agent Run Log: sign-in returns the user to the sign-in page (#84)

> **Date:** 2026-08-09
> **Spec:** none — tech-debt/bugfix item. [Issue #84](https://github.com/maststeiner/store-it/issues/84)
> is the frozen G1 input (repository convention for items derived from findings rather than
> from a feature request).
> **Persona(s):** developer
> **Model:** Claude Opus 5
> **Branch / PR:** `fix/login-return-url`

---

## Task

After signing in with a real Microsoft account against the local container stack, the user
landed back on `/login`. The session was valid the whole time — only the landing was wrong,
which made a working sign-in look like a failed one.

## Cause

`AuthService.login()` sent `returnUrl=location.pathname`, and the provider buttons live on
`/login`. The OIDC callback therefore returned to the page the user had just left. Two
adjacent gaps made it worse: `authGuard` redirected with `parseUrl('/login')` and threw away
the route the user had asked for, and `LoginPage` ignored an existing session.

## What changed

| Change | Why |
|---|---|
| `authGuard` now redirects to `/login?returnUrl=<attempted url>` | The guard is the only place that knows where the user was going. Without it, no downstream fix can return them there. |
| `login(provider, returnUrl?)` takes the target explicitly | `location.pathname` is the wrong source: on the sign-in page it is the sign-in page. |
| `appLocalPath()` narrows any candidate to an app-local path | One predicate for "is this a safe place to send someone", used by both the service and the page. |
| `LoginPage` reads `returnUrl` and skips itself when a session exists | An authenticated visitor on `/login` would otherwise do a silent OIDC round trip back to `/login`. |

`appLocalPath` deliberately duplicates the backend's `SafeReturnUrl` guard (single leading
slash, no `//host`, no `/\host`) instead of relying on it. The backend guard protects the
redirect it performs; this one decides what the SPA is willing to *ask* for, and it adds the
rule the backend has no reason to know — `/login` is not a valid destination.

## Verification

| AC | How it was verified | Result |
|----|---------------------|--------|
| AC-1 | `LoginPage` unit test: click with no `returnUrl` | `login('microsoft', '/')` — the router resolves `/` to the storage list |
| AC-2 | Guard test with `state.url = '/storages/7/items'`; page test with `?returnUrl=/storages/7` | `/login?returnUrl=%2Fstorages%2F7%2Fitems`, then handed to `login()` unchanged |
| AC-3 | Service and page tests with `/login`, `/login?returnUrl=…`, `/login/extra` | all collapse to `/` |
| AC-4 | Table test over `//evil.example.com`, `/\evil.example.com`, `https://evil.example.com`, `storages`, `''`, `null`, `undefined` | all collapse to `/` |
| AC-5 | Page test with a session present, and one with a session already known | `navigateByUrl('/storages/7')`; the known-session case issues no second `/auth/me` |
| Regression | `npm test` | **76/76 pass** (11 files), up from 68 |
| Lint / format | `ng lint`, `prettier --check` | clean |
| Build | `npm run build` | succeeds, 374 kB initial |
| E2E impact | Read `e2e/auth.spec.ts`, `e2e/storage.spec.ts` | unaffected: the anonymous case still lands on `/login` with the provider buttons, and the guard has already resolved the session before `LoginPage` asks, so no extra `/auth/me` is issued |

**Not verified from here:** the real browser round trip through Microsoft — that needs
credentials and a browser, so it is Marcel's check. The failing behaviour it replaces was
observed by him on the running stack.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | Reported the symptom while setting up OIDC on the container stack | Found the defect |
| 2 | *"nun noch gerne umsetzen, dass nach dem login nicht mehr die selbe seite angezeigt wird, sonder die storages"* | Task, and the decision to fix it as its own strand rather than inside PR #83 |

## Outcome

- **Result:** ready for review
- **Deviations:** the fix goes one step past the literal request — it also restores the
  deep-link return the `returnUrl` parameter always implied, because the guard change was
  needed anyway and half of it would have been dead code.
- **Follow-up:** none.
