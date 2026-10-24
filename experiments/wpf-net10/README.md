# Full-source WPF .NET 10 compatibility experiment

This **isolated** SDK-style `net10.0-windows` project compiles all production `../../src/*.cs` (including native winsqlite3 P/Invoke, XML settings, CD/CD-TEXT, metadata engine interop and portable entry point) without copying or modifying the production sources. It is deliberately **not** a migration or default build.

The experiment uses the same pinned public native metadata engine as `.github/workflows/windows.yml` and runs `--self-test-public` only on generated temporary synthetic catalog data. The project itself does not reference user music or private databases. A successful CI build/self-test proves API and synthetic contract compatibility only; physical CD drives, production settings, actual window behavior, DPI, touchpad, startup and resource usage remain separate real-host gates.

The legacy `BuildOnly.cmd` workflow and distributable remain unchanged. Do not merge or release a .NET 10 executable without complete Windows production parity and explicit migration approval. See lxsdd/dev-infrastructure#18 and lxsdd/dj-library-public#4.

The .NET Framework-only `System.Web.Script.Serialization.JavaScriptSerializer` is absent on .NET 10. A **probe-local** `System.Text.Json` compatibility adapter supplies only the members actually used by these sources; a separate synthetic test checks nested dictionaries, arrays, numbers, typed interchange and length bounds. **This is not proven wire-format parity** (DateTime, numeric corner cases, Unicode, depth and unknown members require differential legacy-vs-modern tests). It is never compiled into the legacy production build.
