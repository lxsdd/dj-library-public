# Keyboard and clipboard acceptance boundary (2026-10-19)

Applies only to the isolated Windows A/B experiment. It does not approve
switching DJ Library from WPF to WinUI 3.

## Verified source/runtime guards

- WPF DataGrid is read-only; the real-Windows navigation smoke asserts this.
- WinUI.TableView 1.5.0 is explicitly `IsReadOnly=True`,
  `CanCopy=True`, `CanPaste=False`; the real-Windows WinUI smoke
  asserts all three. Paste is enabled by default in the vendor API, so
  read-only data must not rely on an implicit paste policy.
- Both still use disposable synthetic data, including the test-only
  v4 SQLite CatalogService fixture. No user music, paths or settings.

## Unresolved upstream keyboard-focus hazard

An upstream report, [WinUI.TableView #443](https://github.com/w-ahmad/WinUI.TableView/issues/443),
describes a keyboard focus trap: Tab, Shift+Tab, Escape and F6
cannot move focus out of the table, even after a programmatic
`FocusManager.TryMoveFocus`. This may violate WCAG 2.1.2. The report
tested **1.5.0-preview2** on Windows 11/.NET 10, **not** the pinned stable
1.5.0 in this experiment. Do not incorrectly report the stable version
as confirmed affected or fixed. The report remained open when reviewed.

## Mandatory blocking human-host test

On the same real Windows 10/11 host, with both standalone published
WinUI and WPF test programs and the 100,000-row workload:

1. Put focus on a table row; press Tab repeatedly. Focus must leave the
   table and reach the adjacent toolbar controls, not cycle among cells.
2. Repeat in reverse (Shift+Tab). Navigate back into the table using
   keyboard only; verify visible focus and current selected row.
3. Try Home/End/PageUp/PageDown/Ctrl+Space with multi-selection; verify
   no unexpected loss of selected track identities.
4. Ctrl+C copies selected rows as tabular text, Ctrl+Shift+C includes
   headers. Ctrl+V does not mutate the read-only table or source data.
5. Check accessibility with Narrator and preferably NVDA; verify table,
   row, column names, current focus, selection announcements.
6. Repeat at 100/150/200% DPI and after window resize. Verify focus
   escape, horizontal touchpad scroll, first/last column, and no frozen
   keyboard focus.

A GitHub-hosted compositor/layout smoke **cannot** substitute for these
physical input, accessibility or screen-reader checks. Maintain
`INSUFFICIENT_EVIDENCE_FOR_PRODUCTION_WINUI_CUTOVER` until proven.
