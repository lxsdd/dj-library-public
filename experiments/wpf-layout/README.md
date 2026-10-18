# Real-source WPF geometry and grid persistence contract

Use the **unchanged real production** `src/Settings.cs`, `src/WindowGeometrySettings.cs`, `src/GridLayoutSettings.cs` in the experiment. A minimal fixture supplies only unrelated types for settings serialization and a bare read-only WPF DataGrid constructor; the persistence implementations themselves are not mocked.

On a real Windows GitHub-hosted runner:
1. Build the same three production source files (with inert fixture types) for the legacy .NET Framework 4.8 compiler, and compile them again as an actual .NET 10 WPF executable.
2. Execute existing production `ValidateContract()` checks (geometry offscreen bounds/normal/maximized, named layouts and safe stale-main-window merge).
3. Open an actual WPF Window/DataGrid HWND, reorder columns, set pixel, star and auto widths, hide a column and apply a descending sort.
4. Capture the named layout and geometry through production methods, serialize/deserialize **only in memory** via production `AppSettings` XML, open a *new* WPF window, apply the real persistence code, and verify round-trip order, widths, visibility, sorting and normal window bounds.
5. Check that disconnected-monitor coordinates are clamped to a safe work area.

This test deliberately **does not call** production `SettingsManager.Save`, `Load`, `Update` or `Attach`: those APIs address the real user's roaming profile. It does not open audio, network, DB, user catalog or metadata. It does not modify any product source.

Limitations: WPF runtime success here is only a Windows synthetic HWND + in-memory serialization contract. Real consumer startup/closing event wiring, forced settings corruption, concurrent writes, DPI/dual monitors, portable installation, native touchpad and user-host acceptance remain open and must not be claimed.

This is a release-independent experiment linked to [lxsdd/dev-infrastructure#18](https://github.com/lxsdd/dev-infrastructure/issues/18); do not silently merge/replace existing application settings or window implementations.
