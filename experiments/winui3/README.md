# DJ Library – WinUI 3 A/B prototype (source candidate)

**NOT the DJ Library application.** This is an isolated experimental window. It has no access to a DJ Library SQLite catalog, no foobar Bridge integration, no audio, no rekordbox or file-write code and no private fixtures.

- Desktop: .NET 10, WinUI 3 / Windows App SDK 2.5.1 (stable), MIT WinUI.TableView 1.5.0 (stable).
- Synthetic data sizes: 1,345 / 15,000 / 54,129 / 100,000; deterministic stable object identity.
- Proof surface: dense multicolumn table, built-in sort/scroll/reorder candidate, focus and filter. The displayed stopwatch measures **synchronous data generation / filter / ItemsSource assignment**, not rendering frame time. No speed result is claimed.
- Deployment: unpackaged, self-contained for x64. No MSIX installer or separate runtime is intended. Packaging/startup must be verified on a clean machine.
- Existing `src/`, `BuildOnly.cmd`, catalog schema, settings and Windows CI are unchanged.

## Gates before considering a migration

1. Compile this exact branch in Windows CI; no zero-step or skipped runner can be considered PASS.
2. Prove real window launches, source UI handlers dispatch, table sort preserves stable `Identity`, selection, multi-select, Ctrl+C, Enter, horizontal touchpad, column widths/order/visibility persisted, compact layout, tooltips, dark mode and 100–200% DPI.
3. Benchmark end-to-end visible initial frames, scrolling and search/filter on the **same** synthetic fixture and exact hardware against `dj-library` WPF; measure startup, peak memory, responsiveness and portably deployed size. Use repeated warm/cold runs, medians and p95, not a single stopwatch.
4. Evaluate native Windows CD/CD-TEXT and clipboard APIs separately with safe disposable data. Stage business/viewmodel library only after data and configuration compatibility proofs.
5. Only consider a production UI switch if WinUI performs **meaningfully better** and full feature parity is demonstrated. Retain old WPF build, real-data backup/restore and rollback until user acceptance.

Central coordination: [dev-infrastructure #18](https://github.com/lxsdd/dev-infrastructure/issues/18). Current experiment issue: [DJ Library public #4](https://github.com/lxsdd/dj-library-public/issues/4).

Reference: Microsoft WinUI 3 migration guide: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/migration-decision-guide


## First read-only native DJ Library model integration (2026-10-19)

The WinUI 3 and WPF real-window runtime tests now **compile the unmodified
production `src/CatalogNativeModels.cs`** source as a linked C# file in each
test executable. A shared fail-closed `NativeTrackProjection` maps
`CatalogNativeTrack` records to the exact same presentation row type for
both table frameworks. The records are created in memory, with positive
physical identity, positive disc ID and position, and finite duration;
duplicate IDs/invalid source identities and invalid durations are rejected.

The Windows test has the native models generate 1,345 synthetic DJ catalog
tracks. Each actually rendered WPF DataGrid and WinUI TableView must keep two
*stable catalog IDs* selected across native-model filter/sort/reset. The
regular 100,000-row synthetic performance, scroll, actual UI Automation,
window resize, portable startup, schema/column and snapshot tests still run.

- [PR #5](https://github.com/lxsdd/dj-library-public/pull/5),
  original integration SHA `1d9fcb3e152443b6f0a29abe586d9352daaf3253`.
- [Real WinUI/WPF HWND, native model, and WinUI published-runtime Windows run
  39232911209](https://github.com/lxsdd/dj-library-public/actions/runs/39232911209): SUCCESS.
- [Full existing DJ Library WPF .NET 10 source compatibility
  39232911191](https://github.com/lxsdd/dj-library-public/actions/runs/39232911191): SUCCESS.
- [Legacy build/selftest 39232911216](https://github.com/lxsdd/dj-library-public/actions/runs/39232911216):
  SUCCESS.

**Boundary:** this exercises the **real native model type**, NOT a read-only
SQLite catalog/Bridge consumer and NOT the actual installed DJ Library UI.
No real personal data, database, foobar Bridge, CD drive, music path or native
settings is accessed. Migration stays blocked until the actual repository
consumer, real UI keyboard/touchpad/DPI/read-only deployment and full
feature parity have passed. WinUI 3 is not a drop-in replacement just
because model binding and current performance probes pass.


## Disposable native SQLite `GetNativeTracks()` integration (2026-10-19)

The A/B **real HWND tests** now compile original, unmodified
`src/WinSqlite.cs`, `src/CatalogService.NativeProjection.cs`, and
`src/CatalogNativeModels.cs` directly into both test applications.
`experiments/shared/NativeSqliteFixture.cs` is a **test-only partial
CatalogService host**, *not* the full production bootstrap: it creates a
unique, disposable database under `%TEMP%` with current catalog-v4
release/disc/track table definitions and 1,345 fully synthetic records.

The native WinSqlite driver closes its initial writable setup connection.
The test computes a SHA-256 on the database while its handle is closed,
opens it again, sets `PRAGMA query_only=ON`, verifies the guard by
requiring a write attempt to be rejected, and calls the original
`CatalogService.GetNativeTracks()` SELECT/join/row mapping and native
projection contract. It closes SQLite, hashes the file again, and requires
**exact byte-for-byte equality**. Both already-rendered WPF DataGrid and
WinUI 3 TableView then bind those SQL-derived models, filter/sort/reset,
and preserve two selected database track IDs.

Source-validation SHA `4f244a58f64cbd8621aaf09563ccdaf6672d724d`:
- [Windows real GUI native SQLite A/B and self-contained WinUI runtime
  **39239006283**](https://github.com/lxsdd/dj-library-public/actions/runs/39239006283)
  **SUCCESS**, with logged PASS lines for both WPF and WinUI, including
  the separately published folder-local WinUI executable.
- [Full original DJ Library WPF .NET 10 source
  **39239006355**](https://github.com/lxsdd/dj-library-public/actions/runs/39239006355)
  **SUCCESS**.
- [Legacy product Windows build **39239006301**](https://github.com/lxsdd/dj-library-public/actions/runs/39239006301)
  **SUCCESS**.

**Explicit limit:** this uses the original production
`CatalogService.NativeProjection` SQL but a minimal test-only host rather
than the complete installed `CatalogService` bootstrap/migration/undo
engine. There is **no** real personal catalog access, foobar Bridge,
CD/CD-TEXT, full menu/dialog/keyboard/touchpad parity, clean-host
Windows 10 installation or actual product UI migration. No user data,
private tokens, billing or downloaded testing artifacts are involved.
Production WPF remains unchanged and the safe default pending a
reversible fully featured product pilot and real Windows acceptance.
