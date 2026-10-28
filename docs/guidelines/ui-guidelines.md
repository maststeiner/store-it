# UI Guidelines

> **Owner:** Marcel Steiner (Architecture Stewardship)
> **Stack:** see [`docs/project/tech-stack.md`](../project/tech-stack.md) · design tokens live in `frontend/src/styles.scss` (single design-system file)
> **Last updated:** 2026-10-24 (introduced with SPEC-012)

These guidelines are binding for the Developer Agent and the Reviewer Agent for everything the
user sees: templates, the stylesheet, and the layout tests. They apply the widely used standards
to store-it instead of restating them — the standards are the reference, this page says which
ones count here and with which values.

---

## Standards we follow

| Standard | What we take from it | Level |
|----------|---------------------|-------|
| [WCAG 2.2](https://www.w3.org/TR/WCAG22/) | Accessibility of the web client; the legal baseline in the EU (European Accessibility Act, since 2025-06) | **AA** is the bar; AAA where named below |
| [Apple Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines/) | Touch targets, input behaviour on iOS Safari, safe areas | design reference for phones |
| [Material Design 3](https://m3.material.io/foundations/accessible-design/accessibility-basics) | Target spacing, 4-dp spacing rhythm | design reference where Apple is silent |

When a standard and the approved desktop mockup disagree, the standard wins on narrow screens
and the mockup wins on desktop — the two are reconciled in the responsive block of the stylesheet.

## Layout

- **Reflow (WCAG 1.4.10):** every page and every state of it (forms open, panels open, dialogs)
  is usable at **320 CSS px** width without horizontal page scrolling. The document is never
  wider than the viewport. `overflow-x: hidden` on `html`/`body`/`main` is **not** a fix — it hides
  the defect from users and tests alike.
- **One breakpoint, content-driven:** `@media (max-width: 780px)` is the narrow layout. Add a
  breakpoint only when content demands it, never for a device.
- **Intrinsic sizing over fixed widths:** `minmax(0, 1fr)`, `min()`, `clamp()`, `flex-wrap`,
  `min-width: 0` on grid/flex children that hold text or inputs; `overflow-wrap: anywhere` on
  user-provided text (item names, storage titles, e-mail addresses).
- **Forms and repeated rows use explicit grid areas at every width**, never auto-placement.
  Adding a field or an optional cell (tags, hints) must not move its neighbours — SPEC-012
  O1/O4 (narrow) and O5 (wide: the actions dropped to a second line once a row had tags) were
  exactly this. Each cell gets a class that maps to its area; optional content gets its own
  area or line. Controls that repeat per row (edit, delete) sit at the same place in every row.
  A cell in an `auto` column that must keep its x across rows gets `justify-self: end` (or
  `start`): the column is as wide as the row's widest cell, so a stretched cell starts wherever
  the row's own date or quantity happens to start (CI job 1b went red on 2026-10-27 when a
  seeded date first fell on a single-digit day).
- **Zoom (WCAG 1.4.4):** the viewport meta stays `width=device-width, initial-scale=1` — no
  `maximum-scale`, no `user-scalable=no`. Inputs and selects are **≥ 16 px** on narrow screens so
  iOS Safari does not zoom on focus.
- **Safe areas:** fixed or sticky chrome (header, overlays) respects `env(safe-area-inset-*)`
  when it touches a screen edge.

## Touch targets and controls

- **Tap targets ≥ 44 × 44 CSS px on narrow screens** (Apple HIG; WCAG 2.5.5 AAA) for every
  button, link styled as a button, select and icon control; **≥ 24 × 24** everywhere (WCAG 2.5.8
  AA). Adjacent targets keep **≥ 8 px** spacing (Material).
- **Inline text chips** (tag chips on item rows) are the one allowed exception below 44 px: at
  least 32 px high, and the same action must be reachable through a 44 px control elsewhere.
- **Icon glyphs ≥ 18 px** on narrow screens; every icon-only control has an `aria-label` and a
  `title` (existing rule, SPEC-001).
- **Focus is visible** (`:focus-visible` outline, existing token) and the order follows the
  visual order.

## Colour and contrast

- **Text ≥ 4.5 : 1**, large text and **non-text UI (icons, borders, focus rings) ≥ 3 : 1**
  (WCAG 1.4.3 / 1.4.11) against the panel they sit on — translucent panels are measured against
  the page background they blur.
- Colour never carries meaning alone: status uses the dot/label pair, not the tint only.
- `prefers-reduced-motion` disables transitions (existing rule).

## Tests

- **Layout facts are asserted in a real browser** (Playwright project `mobile`, Chromium mobile
  emulation at 390 × 844 and 320 × 568): document width ≤ viewport, bounding boxes of controls
  inside their container, target sizes, computed font sizes, row structure. jsdom has no layout
  engine — never assert layout in a vitest spec.
- **axe-core (`@axe-core/playwright`) runs on every page state the mobile tests visit** with the
  tags `wcag2a`, `wcag2aa`, `wcag21aa`, `wcag22aa`; **zero violations** is the gate. A rule may be
  excluded only with a comment naming the WCAG criterion and the reason, and a tech-debt issue.
- **Desktop appearance is guarded by the existing desktop project**; a narrow-screen fix must not
  change the approved desktop layout.
- Visual-regression screenshots (pixel diffs) are deliberately **not** used: they break on every
  intended change and do not say what is wrong. Geometric assertions do.

## Checklist for the Reviewer Agent

1. Any new element inside `.item-row`, `.item-edit`, `.add-form`, `.detail-head`, `header`?
   → Does it have a grid area / wrap rule on narrow screens, and does the mobile spec cover it?
2. Any new button, chip or select? → 44 px on narrow screens, 24 px everywhere, label + title.
3. Any new input? → 16 px on narrow screens, `min-width: 0`.
4. Any new page or panel? → added to the mobile test's list of states (reflow + axe).
5. Any `overflow: hidden`, fixed pixel width or `white-space: nowrap`? → justify or remove.
