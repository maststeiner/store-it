# store-it — Brand assets

Reusable logo assets. This folder is the **source of truth**; deployed copies
(e.g. the web favicon) are derived from these files.

## Files

| File | Use |
|------|-----|
| `store-it-icon.svg` | **App icon / favicon** — the mark on a rounded gradient tile (self-contained). Master for generating iOS/Android PNG icons. |
| `store-it-mark.svg` | **Mark only** — the drawer stack in gradient, no tile. Use on light surfaces where a tile isn't wanted. |

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
- **iOS app (planned):** export `store-it-icon.svg` to the required PNG sizes.
