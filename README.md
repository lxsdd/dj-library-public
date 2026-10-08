# DJ Library — public source

Windows/WPF application for **your own local** CD collection and digital-music metadata. Public code contains no owner music library, private track lists, audio, sample catalog, match index or credentials.

## How to use your existing catalog

On the same Windows user account, the application automatically opens `%APPDATA%\DJ Library\catalog-v0.4.sqlite`. Do not move or upload your database. A first-time user gets an empty writable catalog. The Catalog Manager has local **Backup** and **Restore** and can import `.sqlite`, `.bak`, `.sqlite.gz` or `.sqlite.gz.b64` files. Restore replaces the current catalog only after user confirmation and maintains a pre-restore backup. The foobar bridge is optional and read-only.

## Build

`BuildOnly.cmd` uses the Windows .NET Framework/WPF C# compiler and produces `DJLibrary.exe`. For full functionality the `djmeta_native.dll` and `rules/default-rules.json` from the public [DJ Metadata Normalizer](https://github.com/lxsdd/dj-metadata-normalizer) are included by the Windows CI build.

Every build in this public lineage runs **synthetic SQLite and CDX tests** without accessing any personal database. No real dataset is ever packed in the portable ZIP. The application can be used with any supported user-supplied local SQLite catalog. Additional interchange adapters are planned.

The prior source repository and its historical real-data CI fixtures remain **private** and are not part of this repository's ancestry. Historical v0.4 performance acceptance on an owner's catalog is **not** claimed by this new public synthetic smoke. Do not commit catalog exports, bridge snapshots, logs, tokens or user music files.

This migration branch is initially an unqualified source candidate until the Windows public Actions job succeeds and its generated binary is tested with a local catalog.
