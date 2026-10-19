# DJ Library: WPF vs WinUI 3 decision record

**Decision at 2026-10-19: INSUFFICIENT_EVIDENCE_FOR_PRODUCTION_WINUI_CUTOVER.**
Keep production WPF unchanged. This document is an experimental-branch
checkpoint, not a product release, product migration authorization, or an
approval to rewrite a customer's data.

## Exact evidence baseline

- Draft comparison: https://github.com/lxsdd/dj-library-public/pull/5
- Completed pre-decision source SHA: `550ac6f3741c559e5125283f88b8502be6463a48`
- On that exact SHA, public Windows Actions succeeded:
  - GUI/WPF/WinUI + separately published self-contained smoke: run `39268131365`
  - Full legacy-source WPF .NET 10 compatibility: run `39268130820`
  - Unchanged legacy Windows product build/self-test: run `39268130948`
- Earlier GUI A/B `66d6690063464379c149f185f72e568d595dd4ef`
  also passed three SHA-bound runs, including empty release and stale
  navigation-state negative tests (GUI run `39266943414`).
- Current **experiment** uses .NET 10, Windows App SDK `2.5.1` and
  community `WinUI.TableView` `1.5.0` (baseline); candidate `1.5.1` is being independently qualified, alongside WPF DataGrid.
  Native foobar2000 plugins and Android are separate stacks.

## What is actually qualified

- Same 1,345 / 15,000 / 54,129 / 100,000 synthetic track fixtures,
  deterministic identities, filter/sort/multi-select, virtualized scrolling,
  real-Windows resized HWND and UI Automation Grid/Selection/Table probes.
- Original linked DJ Library `CatalogService` and `WinSqlite` sources
  queried real disposable SQLite v4 fixtures under TEMP. Native track
  SELECT exercised 54,129 records. `PRAGMA query_only`, blocked writes
  and database SHA-256 checks protect test read operations.
- Release -> multi-disc -> track navigation and empty-release/return
  transitions were exercised on real WPF/WinUI tables with stored
  synthetic TOC/CD-TEXT/CDX details and stable row identities.
- Self-contained x64 WinUI folder execution succeeded in Windows CI.
  Layout/column tests and WPF/WinUI `.NET 10` builds passed.
- WinUI explicit `CanPaste=False`, `CanCopy=True`,
  `IsReadOnly=True` and WPF read-only controls are runtime asserted.

## What remains unqualified

- Fully integrated production DJ Library window, menus, filtering,
  pointer/keyboard command model, workflow/metadata engine, live foobar
  Bridge v3 consumer, real settings persistence/recovery and optical
  CD/CD-TEXT hardware, not just catalog data projections.
- Clean user's Windows 10/11 host, physical precision touchpad,
  DPI 100/150/200%, clipboard/drag-drop/shortcut behavior and genuine
  screenreader announcements under Narrator/NVDA.
- A specific unresolved upstream accessibility risk:
  https://github.com/w-ahmad/WinUI.TableView/issues/443
  reports a keyboard trap in **1.5.0-preview2**. Applicability to the
  stable **1.5.0** and **1.5.1** is unknown. See
  `experiments/winui3/INPUT-ACCESSIBILITY-GATE.md`.
- Screen-perceived frame latency, cold-start distributions,
  high-percentile jank and long-running resource use on identical
  user hardware. Synthetic CI compositor callback/memory samples
  **do not** justify declaring WinUI faster in production.
- Legacy serializer binary/wire compatibility for nontrivial
  user settings/JSON is not established by the probe-local adapter.
- Neither this branch nor any other experiment grants real-data write
  or product cutover permission.

## Decision options and controls

1. **RETAIN WPF (current safe operating choice).** Existing product
   remains the standard; keep independent WPF build/CI intact.
2. **ADOPT REVERSIBLE WINUI PILOT** only after all functional,
   accessibility/input, packaging, settings/bridge and hardware gates
   pass on the actual host and user expressly approves the switch.
3. **INSUFFICIENT EVIDENCE** means keep developing independently
   within this draft without claiming a production GO.

Any pilot must preserve a tested rollback to the old WPF binary and
settings and require a backup/restore strategy, compatibility and
data-integrity checks before first access to a real catalog. No live
catalog/music/rekordbox/tag/setting writes without explicit permission.
No private Actions billing, extra paid runners, huge artifact uploads,
secrets, proprietary data, or new automation.

## Later isolated candidate (pending exact-head CI)

The `1.5.1` TableView maintenance update adds fixes for column
resizing/scrolling/header alignment; independent Windows CI and a
bounded keyboard focus-escape diagnostic must qualify the experiment.
This does not approve production migration. Upstream #443 stays open.

## Focus diagnostic interpretation (2026-10-19)

The exact-head GUI run `39274187587`, SHA `fe4d469ccc6c71a51ef16ebc3774afdea3fdf311`,
verified 24 **routed** Tab events reached each active table. Both WPF
DataGrid and WinUI.TableView 1.5.1 remained focused inside their
100,000-row grids after 24 forward Tab presses. This alone is **NOT**
evidence of a WinUI-specific keyboard defect: focus can legitimately
traverse very many editable/focusable cells before leaving.
Further tests must compare backward-from-first-cell and direct
post-table focus transfers, not mistakenly treat large forward
cell traversal as a proven WCAG trap. Upstream #443 still warrants
real host keyboard and screen-reader validation.
