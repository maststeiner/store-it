# Agent Run Log: translations cached across releases; item-form buttons (#184)

> **Date:** 2026-09-29
> **Spec:** none — bug issue [#184](https://github.com/maststeiner/store-it/issues/184) is the frozen input (bug class, no spec)
> **Persona(s):** developer
> **Model:** Claude Fable 5.1
> **Branch / PR:** `fix/i18n-cache-and-form-actions` → PR linked from #184

---

## Task

Marcel's local test of #183 showed the raw key `items.new` on the new button and the Add/Cancel
buttons squeezed side by side. Find out why the translation was missing although the key is in
every dictionary; fix the layout as he asked ("Hinzufügen und Abbrechen untereinander").

## Plan

1. Verify the key is committed and served; read how the client loads dictionaries and how nginx
   caches assets.
2. Fix the cache policy in nginx and verify the headers with a throwaway container; add a
   client-side cache buster for caches filled before the header existed.
3. Stack the buttons; run the frontend checks; new PR (the original PR #183 had been merged
   minutes before these commits — they were orphaned on its branch and cherry-picked here).

## Key Decisions

- **Root cause is caching, not the dictionary:** `nginx.conf` lists only hashed artefacts as
  immutable and `index.html` as `no-store`; the unhashed `/assets/i18n/*.json` got no policy at
  all, so browsers apply heuristic freshness. Every release therefore shows stale texts or raw
  keys to returning users until their heuristic cache expires — prod included since `v0.2.0`.
- **Two layers:** `Cache-Control: no-cache` (ETag revalidation, a 304 per load) for the future,
  plus a per-app-start `?v=<stamp>` on the URL, because an already-cached response without the
  header is not consulted with the server again until it expires — only a new URL gets past it.
- **Why a stamp per app start and not a build hash:** Angular does not hash `public/` assets and
  the project keeps no build-time version constant; the stamp costs at most one 304 per session.
- **Process note:** two fix commits were pushed to `feature/add-item-on-demand` after PR #183 had
  already been merged (18:48); GitHub does not add later commits to a merged PR. Lesson for the
  agent: check the PR state before pushing follow-ups to a branch the human may merge any time.

## Human Interventions

| # | Intervention | Reason |
|---|--------------|--------|
| 1 | "der button heisst items.new, also keine Übersetzung. Und der Abbrechen Button ist komisch hinter dem Item … Hinzufügen und Abbrechen untereinander" | Bug found by the human test; layout wish. |
| 2 | "der button heisst immer noch items.new" (after the first fix, which had not reached develop) | Led to the cache buster and to noticing the orphaned commits. |

## UX refinement in the same PR (Marcel, 2026-09-29)

"Bei den Lagerorten gibt es nun zwei Möglichkeiten … Ich würde den Button entfernen. Bei den
Artikeln hätte ich einen Icon-Button mit einem Plus gemacht neben dem Edit, Teilen und Papierkorb."
Done: the list keeps only the placeholder card; the detail header gets a ＋ icon button that
opens and closes the form (`aria-expanded`), the text button is gone; `storages.new` removed from
the dictionaries, `items.new` is the icon's label. SPEC-001 A4 reworded. The retry test for the
dictionary loader also had to flush the English fallback request — a failing test slipped into
one push before that; fixed in the next commit.

## Verification

nginx headers verified in a container (json → `no-cache` + ETag; js → `immutable`; index.html →
`no-store`); frontend 157 vitest green, lint, prettier, `ng build`. Human re-test on the container
stack pending (hard reload once, then the label must read "+ Neuer Artikel").

## Outcome

- **Result:** PR opened against `develop`.
- **Deviations from spec:** n/a.
- **Harness follow-up:** none.
