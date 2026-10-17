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
