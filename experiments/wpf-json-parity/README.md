# Original JavaScriptSerializer vs .NET 10 adapter

Isolated, read-only DJ Library JSON interchange compatibility study.
On free public Windows CI, compile and run identical synthetic harness code:
- Original `System.Web.Script.Serialization.JavaScriptSerializer` on .NET Framework 4.8.
- Unchanged experimental .NET 10 `LegacyJsonCompat.cs` copied by Git blob from
  existing WinUI experiment SHA `2d1fdfaeeb83409211ae54f04a3411af09d5a421`.

Compare deterministic reports of parsed number **values and runtime types**,
nested dictionaries, lists, bools, null, Unicode, representative typed
CatalogInterchange Format/Version/Releases/Tracks and JSON length caps.
Any difference fails CI. No catalog, profile, music or APPDATA input;
only explicitly named RUNNER_TEMP output files. No product merge.

A pass is **not** wire-format equivalence across all historical files:
DateTime Microsoft AJAX encodings, recursion and exotic floating-point
values, custom fields and arbitrary HTTP payloads remain unqualified.
WPF stays production; this doesn't revive the rejected WinUI option.
