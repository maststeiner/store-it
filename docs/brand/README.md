# store-it — Brand assets

Reusable logo assets. This folder is the **source of truth**; deployed copies
(e.g. the web favicon) are derived from these files.

## Files

| File | Use |
|------|-----|
| `store-it-icon.svg` | **App icon / favicon** — the mark on a rounded gradient tile (self-contained). Used as the web SVG favicon. |
| `store-it-icon-square.svg` | **Full-bleed square app-icon master** (no rounded corners — iOS/Android round themselves). Source for the raster home-screen PNGs. |
| `store-it-mark.svg` | **Mark only, light bg** — the drawer stack in gradient, no tile. |
| `store-it-mark-dark.svg` | **Mark only, dark bg** — light drawer fills, no tile. |
| `store-it-lockup.svg` | **Logo + wordmark, light bg** — horizontal lockup (mark + “store-it”). |
| `store-it-lockup-dark.svg` | **Logo + wordmark, dark bg** — light wordmark. |

> **Wordmark font:** the lockups render the text with an Avenir Next / Segoe UI / system-ui sans stack (weight 600), matching the app. For pixel-fixed output (print, third-party tools without that font) outline the text to paths first.

> **Wordmark is invariant:** "store-it" is the product/brand name and is **never localized** — it renders identically in de/en/fr/it. The web header therefore hard-codes it rather than routing it through the TranslateService.

The concept: three stacked drawers/shelves with handle notches — a storage unit
that also reads as a stack ("store-it"). Chosen concept H2.

## Palette

| Token | Hex | Use |
|-------|-----|-----|
| Teal (accent) | `#3E9C93` | gradient start, primary accent |
| Blue (accent-2) | `#6FA8D6` | gradient end |
| Handle teal | `#2C7B73` | handle notches on the white icon |
| Ground | `#EDF5F2` | app background |
| Text | `#16343A` | text, dark surfaces |

Gradient: `linear-gradient(135deg, #3E9C93 → #6FA8D6)`.

## Usage

- **Minimum size:** the icon reads down to ~20 px; below that prefer a solid tile.
- **Clear space:** keep padding of ~1 bar-height around the mark.
- **On dark:** use the white/light drawer fills (see the web dark header).
- **Web:** the app serves a copy at `frontend/public/logo.svg` (favicon + header).
  When the logo changes here, update that copy too.
- **Web home-screen icon:** `frontend/public/apple-touch-icon.png` (180×180) is generated from the square master. Regenerate after a logo change:
  ```sh
  sips -s format png -z 180 180 docs/brand/store-it-icon-square.svg \
    --out frontend/public/apple-touch-icon.png
  ```
- **iOS/Android app (planned):** export `store-it-icon-square.svg` to the required PNG sizes (full-bleed; the OS masks the corners).
