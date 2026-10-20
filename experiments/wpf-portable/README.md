# Complete DJ Library WPF .NET 10 portable-folder deployment gate

This is an isolated Draft PR and not a product migration or release.

**Original source:** The .NET 10 SDK WPF project links every unchanged
`src/*.cs` on the public DJ Library `main` branch. Its only adapter
is the unchanged `LegacyJsonCompat.cs` from an existing experiment,
separately cross-runtime-qualified in public Draft PR #8. The legacy
`BuildOnly.cmd` remains production.

**Deployment proof on free public Windows-2022 Actions:** publish an actual
`win-x64` self-contained WPF folder; rebuild the pinned public
`lxsdd/dj-metadata-normalizer` source SHA
`365432ad8d6d0b80cc138c21e0d8fd8edca9e499`;
include `djmeta_native.dll` and `rules/default-rules.json`; check that
the native library really loads and `djmeta_abi_version() == 1`.
Check presence of `hostfxr.dll`, `coreclr.dll`, managed WPF
`PresentationFramework.dll` and the application runtime config.

Copy the complete published folder to a **different path containing
spaces** and invoke the actual relocated executable from an unrelated
working directory, using exclusively `--self-test-public`. The original
product self-test exercises a disposable TEMP SQLite v4 catalog:
synthetic release/disc/track CRUD, native projection, CDX/TOC
classification, open-WAL import, gzip/base64 support, JSON export/import
roundtrip, invalid-foreign-catalog rejection and rollback. It deletes
its generated TEMP catalog. The report is written next to the relocated
executable, never the caller's working directory.

**Actual visible GUI startup on a disposable hosted runner:** After
portable-mode qualification, a separate process launches the genuine
unchanged DJ Library MainWindow with a **fresh EMPTY catalog** in the
ephemeral GitHub runner profile. Before launch it requires the DJ Library
application-data directory to be absent and verifies it belongs to
the hosted runner's USERPROFILE; any preexisting directory immediately
fails closed. It then verifies an actual visible top-level WPF HWND
with the exact Native v0.4.0 title and a newly initialized SQLite
catalog, force-stops the test process without invoking production
settings save hooks, and deletes ONLY the directory which it proved
was initially absent. No such startup is performed on a user's PC.

**Strict safety boundary:** Neither branch test opens owner data, music
files, rekordbox, a real foobar profile, optical hardware, credentials,
private Actions runners or live external metadata endpoints. No
artifacts are uploaded, no releases or merges, no local installation.
The synthetic SQLite/JSON test continues to use runner TEMP exclusively;
the real GUI startup is limited to the newly created throwaway hosted
Windows runner profile, deleted immediately after the test.

**Still open:** hands-on foreground UI interactions, external CD/TOC/
CD-TEXT hardware, actual production native source behavior, mixed DPI,
touchpad and multi-monitor, backup/copy of non-synthetic user profiles,
historic exotic JSON wire cases, real metadata source credentials and
end-user device acceptance. A passing CI is not a production GO.

## External real-window UI Automation integration (2026-10-20)

To move beyond a visible-window-only smoke, the separate net10 WPF
`GuiInteractionProbe` inspects the **real running unmodified product**
through UI Automation on a fresh disposable public Windows runner.
It verifies exactly two real `Tracks`/`CDs` TabItems,
switches both via UIA SelectionItemPattern, then injects real
foreground Ctrl+2 and Ctrl+1 keys and verifies the selected tab.
Ctrl+F must focus the actual editable search field, whose synthetic
text must be cleared by Escape; native window resize/restore must
preserve visible tabs. There are no imported real music/CD records.

The hosted runner's user-profile `DJ Library` directory must be
absent before startup; otherwise the test fails closed. The temporary
empty catalog is removed after forcibly stopping the WPF process.
These operations do **not** prove a real physical keyboard or
Precision Touchpad, track navigation with populated data, audio
playback, mixed DPI, CD hardware or the installed end-user profile.

## Real save/restore lifecycle qualification

The same ephemeral hosted-runner UI Automation probe also selects CDs
and closes the unchanged original MainWindow using the real WPF
WindowPattern, instead of force-killing it. The test checks that
`MainClosing` persisted a **new synthetic** `settings.xml` containing
`SelectedTab=1` and nonempty actual Tracks/CDs column layouts.
It then launches a second original MainWindow against only this
throwaway runner profile and requires the CDs tab to be restored and
the second window to close normally. The profile directory is proven
absent before the first launch and removed after both processes stop.

This is a **real integration check** of save, load, close and reopen
under .NET 10, beyond XML serializer-only tests. It does not inspect,
modify or back up the user's installed settings or catalog, and
doesn't establish cross-DPI geometry restoration on physical hardware.

## Real populated Track/CD table and search test (disposable CI)

After both clean-profile save/reopen sessions have closed, an isolated
bootstrapping executable links the **unchanged real DJ Library
`CatalogService`** and writes **only** to the same disposable hosted
Windows runner's initial empty SQLite catalog. It refuses a nonempty
catalog, different APPDATA path or non-GitHub environment. The fixture
uses one artificial release, two physical discs with synthetic TOCs and
two tracks (`Photon Fixture Only`, `Nebula Fixture Only`).

The third normal application launch checks the **actual visible WPF
DataGrid UI Automation GridPattern** contains two CDs and two Tracks.
It then tests the real search/filter pipeline: two tracks -> one for
`Photon Fixture Only` -> zero for nonexistent text -> two again
after Escape. It returns to the CDs view and confirms two discs
remain. This verifies UI handling with *populated* synthetic records
but intentionally does not exercise physical optical hardware,
real user artist data, Discogs, foobar, or image/metadata downloads.
The same test-created profile is removed after a regular GUI close.

## Actual Framework 4.8 ↔ .NET 10 GUI/profile cutover and rollback

This further gate compiles the **unchanged original DJ Library WPF
application with BuildOnly.cmd** under .NET Framework 4.8 *on the same
disposable public Windows runner*. It stages the binary separately
alongside the same pinned native metadata DLL and rules used by the
qualified .NET 10 portable bundle.

With an initially **absent** `DJ Library` APPDATA folder (otherwise
fail closed), a real Framework GUI creates an empty catalog and
saves its actual `settings.xml` through normal MainWindow close.
A separate, source-linked .NET 10 sidecar creates exactly one made-up
release, two CDs and two Tracks in this new throwaway catalog. The
original Framework **real GUI** must show and filter those records
using external UI Automation. When all processes are stopped the
runner creates a cold copy of this *synthetic-only* profile and
compares SHA-256 hashes of the catalog and settings against the copy.

The original .NET 10 portable GUI then opens **the same profile**,
shows the same 2 CDs and 2 Tracks and successfully searches and
closes normally. The profile is subsequently restored in full
from its cold snapshot. The catalog and settings must match
their original pre-upgrade SHA-256 values **byte-for-byte** before
the original Framework WPF GUI is reopened for the final UIA
search/selection test. A broken adapter, schema migration or
incompatible settings file must fail the CI gate.

**No production cutover, user file, profile, catalog, installed app,
CD drive, metadata credential, private runner or artifact upload.**
The test is guarded with `GITHUB_ACTIONS=true`,
`RUNNER_ENVIRONMENT=github-hosted` and a profile-path validation,
and exits rather than touching any pre-existing DJ Library profile.
The test-created runner-only directory and GUID backup are cleaned
after processes stop.
