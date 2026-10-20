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

**Strict safety boundary:** normal GUI startup is prohibited in CI,
because that could discover/initialize a runner profile catalog.
The test never opens actual user data, musical files, rekordbox, a
foobar profile, optical hardware, credentials or public network
metadata endpoints; it does not upload build artifacts, publish
packages, change installed products, merge or charge private
GitHub Actions minutes.

**Still open:** actual Windows foreground WPF UI startup, real external
CD/TOC/CD-TEXT drive and native runtime on hardware, mixed DPI,
touchpad/multi-monitor, non-synthetic profile/settings migrations,
historic exotic JSON wire cases, actual metadata source access and
end-user acceptance. A passing CI is not a production GO.
