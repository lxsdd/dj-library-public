# Real Windows horizontal precision-wheel contract (isolated)

This test uses the **actual, unchanged production WPF sources** for `DJLibrary.HorizontalScrollSupport` and `RekordboxMyTagSync.HorizontalScrollSupport`, built together in one .NET 10 test application. No proxy/mock wheel handler.

On a real Windows runner it creates one WPF HWND/DataGrid per product, calls the product `Enable(DataGrid)`, locates an actually horizontally scrollable `ScrollViewer`, sends native `WM_MOUSEHWHEEL` via `user32.SendMessage`, checks +120/-120, zero delta and upper bound and closes the window to exercise hook cleanup. Additionally the same two production source files are compiled by the .NET Framework 4.8 compiler with official Windows desktop references. This verifies **the common source intersection** but does not modify either product or force runtime migration.

## Evidence and important limits

- Pin DJ Library source to this branch's base and MyTagSync to the exact public commit listed in the workflow. Updates require review, not floating `main`.
- GitHub Actions must actually execute compile **and WPF runtime smoke**; a red gate is not silently waived. Reports prove injected native message routing on the CI desktop; they **do not** prove genuine hardware precision-touchpad gesture reception, nested control under pointer, Shift+wheel, right-to-left semantics, DPI, actual app window wiring, or end-user acceptance.
- No real file, track, disk, database, music, network API or tag access; all rows are synthetic and the test does not persist settings.
- The shared implementation may be extracted to a single versioned source artifact only after both producers' licensing, identity, event subscription cleanup and install paths are reviewed; no direct product files altered in this experiment.
- Related governance: https://github.com/lxsdd/dev-infrastructure/issues/18 and the private infrastructure smoke in draft PR #19, whose GitHub CI currently fails before entering steps (likely repo Actions admission, not proof of failing C# compilation).

## Two-tab actual Windows regression (2026-10-19)

This branch now additionally instantiates *two actual virtualized WPF DataGrid
controls* inside a real TabControl for **each** linked, original producer
implementation. For each source it verifies that the hidden second grid's
Loaded event attaches correctly, repeated Enable calls remain idempotent
(one WM_MOUSEHWHEEL notch produces only one routing step), switching
Tracks ↔ CDs targets the visible grid without scrolling the other grid,
selection identities survive the switch, and horizontal offset can be
reset to the first columns. This tests the exact shared source mechanics
without editing either production application or forcing a shared DLL.

These are injected HWND messages on a hosted Windows desktop. They do not
simulate a physical Precision Touchpad or fully establish real host
scrolling/vertical routing, mixed DPI, multi-monitor geometry or product
release acceptance. The currently public files have no explicit LICENSE,
so a separately published redistributable shared package needs its own
verified rights/provenance decision before any wider distribution.
