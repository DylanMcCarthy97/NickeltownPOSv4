# Nickeltown POS — UI rules (TCXWave / 1024×768)

These rules keep layouts stable on the club touchscreen POS. Follow them for new screens and when fixing layout bugs.

## Target display

- **Resolution:** 1024×768 (landscape touchscreen).
- **Theme:** Light POS theme is primary (reduces glare on TCXWave panels).
- **Input:** Touch-first; mouse/keyboard are secondary.

## Layout

- **No outer page scroll** unless unavoidable (e.g. migration wizard). Prefer fixed regions: header, body, bottom actions.
- **Bottom nav / action bars** stay fixed and visible; they must not overlap scrollable content.
- **Touch targets:** Large buttons and list rows; avoid tiny controls or cramped grids.
- **Text:** Use trimming or wrapping so labels are not clipped; do not rely on overflow hidden without `TextTrimming`/`TextWrapping`.
- **Product / item images:** Fixed bounds in cards and detail headers (`MaxWidth`/`MaxHeight`, `Stretch="Uniform"`). Images must not blow out row or card layout.

## Keyboards and numpads

- Reuse **`IInputOverlayService`** (shared keyboard/numpad overlays) instead of the default Windows touch keyboard where the rest of the app does.
- Stock search, money fields, and tab amount entry should follow existing overlay patterns.

## Navigation and chrome

- **Do not duplicate** sign-out or primary navigation buttons on child pages; use shell/workspace chrome.
- Preserve existing flows: **Square**, **tabs**, **stock**, **pitstop**, **reports**, **settings**.

## Workspaces

| Area | Notes |
|------|--------|
| Tabs | Board + slide panels; session targets the selected tab. |
| Add drinks / funds / edit tab | Slide panels; tab identity from workspace selection. |
| Stock management | Fixed browser + summary; edits in modals. |
| Pitstop retail | Catalog + cart + checkout overlays (cash/card/Square). |

## When changing XAML

- Test at **1024×768** before considering a layout done.
- Avoid introducing a page-level `ScrollViewer` around the whole workspace unless required.
- Keep binding names on view models stable when refactoring code-behind.
