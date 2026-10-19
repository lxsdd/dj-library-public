# WPF vs WinUI 3 synthetic A/B baseline

Both experiments compile **the exact same** `../shared/SyntheticCatalog.cs` source.
The model contains only generated identities, artist/title/genre/year/duration.
It does not read or write catalog, music, SQLite, bridge, configuration or user files.

Run `dotnet run --project experiments/synthetic-contract/SyntheticContract.csproj -c Release`
to verify identity uniqueness, determinism, filter and sorting across all four sizes.
On Windows with .NET 10, build/run `experiments/wpf-ab/DJLibrary.WpfAb.csproj`
and `experiments/winui3/DJLibrary.WinUiSpike.csproj` independently.

The buttons perform the same generator/filter/sort operations on the same row identities.
Their synchronous timers measure only operation + ItemsSource assignment; they do **not**
measure first visible frame, scroll frame rate, real accessibility, UI automation,
physical touchpad, cold startup, packaging, or user-visible latency.
Do not rank frameworks from these stopwatch values.

**Outstanding**: instrument visible frame after dispatcher/layout, repeat warm/cold
runs on the same Windows machine, capture median/p95, private working set, DPI
100/150/200%, selection/focus/keyboard/horizontal wheel/column persistence,
dark mode and deployment. Do not replace production WPF based on compile PASS.

## Equal-width A/B correction (2026-10-19)

The previous WPF baseline used six **160px** columns while WinUI's
TableView used six **250px** columns, causing unequal horizontal overflow
and realized-column counts despite identical 1000×840 window requests.
That was **not** an equal-layout performance comparison.

The experiment now explicitly checks six 250px pixel-sized columns
in both real windows and uses the same synthetic fixture sizes. The
existing scroll/resize/selection/metadata/navigation gates remain
mandatory. WPF, like WinUI, may virtualize horizontal UIA column
headers: require valid logical Grid shape and record the subset of
**currently realized** headers, without inventing offscreen peers.

Even with corrected column widths, actual row templates, typography,
measurement pipeline and hardware rendering remain different. A/B
compositor callback latency is **not** end-to-end input-to-photon
latency, and no production winner can be declared from CI samples.
