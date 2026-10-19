// Isolated real-source DJ Library catalog bootstrap and native SQLite UI fixture.
// Every writable database path is generated INSIDE a new private %TEMP% directory.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DJLibrary;

namespace DJLibrary
{
    // TEST-ONLY guard: any accidental production-default-catalog lookup fails
    // before access to local/roaming user application settings.
    internal static class SettingsManager
    {
        public static string SettingsDirectory
        {
            get { throw new InvalidOperationException("User profile catalog/settings access forbidden in WinUI A/B fixture"); }
        }
    }

    // CatalogModels depends on this display helper; the REAL production
    // CatalogService, its migration, undo, serializer and SQL are unchanged.
    public static class UiHelpers
    {
        public static string FormatDuration(double seconds)
        {
            if (seconds <= 0) return "—";
            var duration = TimeSpan.FromSeconds(Math.Round(seconds));
            return duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"m\:ss");
        }
    }

    public sealed partial class CatalogService
    {
        // Test-only partial extension: switch ORIGINAL private native SQLite
        // handle to read-only query mode after full production startup and
        // schema migration/validation has completed.
        internal void EnableQueryOnlyForExperiment()
        {
            _db.Execute("PRAGMA query_only=ON");
            var value = Convert.ToInt32(_db.Scalar("PRAGMA query_only"), CultureInfo.InvariantCulture);
            if (value != 1) throw new InvalidOperationException("Read-only SQLite guard not enabled");
        }
        internal bool VerifyWriteIsRejectedForExperiment()
        {
            try
            {
                _db.Execute("INSERT INTO release(album_artist,album) VALUES('FORBIDDEN','WRITE')");
            }
            catch (InvalidOperationException) { return true; }
            return false;
        }
    }
}

namespace DJLibrary.SyntheticComparison
{
    public static class NativeSqliteFixture
    {
        // Hard-coded count bound, no user-supplied database paths or credentials.
        // This invokes the FULL original production CatalogService constructor,
        // schema-v4 bootstrap, ValidateAndMigrate and GetNativeTracks.
        public static List<SyntheticTrackRow> LoadReadOnlySqlite(int count)
        {
            if (count < 1 || count > 15000)
                throw new ArgumentOutOfRangeException(nameof(count));
            string dir = Path.Combine(Path.GetTempPath(),
                "DJLibrary-WinUi-Full-Catalog-A-B-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "synthetic-native.sqlite");
            try
            {
                using (var schema = CatalogService.CreateEmptyForTesting(path))
                {
                    if (schema.QuickCheck() != "ok" ||
                        schema.GetCounts().Tracks != 0)
                        throw new InvalidDataException("Full CatalogService did not bootstrap a clean schema-v4");
                }

                // Bulk synthetic data insertion via unchanged native WinSqlite
                // API; a single transaction avoids per-row fsync overhead.
                using (var writer = new WinSqliteDb(path))
                {
                    writer.Execute("PRAGMA foreign_keys=ON");
                    writer.Transaction(() =>
                    {
                        writer.Execute("INSERT INTO release(id,album_artist,album,total_discs) VALUES(1,'Synthetic DJ','Synthetic Readonly SQLite',1)");
                        int discs = (count + 11) / 12;
                        for (int d = 1; d <= discs; d++)
                            writer.Execute("INSERT INTO disc(id,release_id,disc_number,medium) VALUES(?,1,1,'Digital')", d);
                        for (int i = 1; i <= count; i++)
                        {
                            int disc = ((i - 1) / 12) + 1;
                            int ordinal = ((i - 1) % 12) + 1;
                            writer.Execute("INSERT INTO track(id,disc_id,position,artist,title,release_date,genre,duration_seconds) VALUES(?,?,?,?,?,?,?,?)",
                                i, disc, ordinal,
                                "Native Artist " + (i % 131).ToString("D3", CultureInfo.InvariantCulture),
                                "Native Title " + i.ToString("D6", CultureInfo.InvariantCulture),
                                "2026-10-19", i % 2 == 0 ? "Trance" : "House",
                                180 + i % 70);
                        }
                    });
                    if (writer.QuickCheck() != "ok")
                        throw new InvalidDataException("Full CatalogService test database quick_check failed");
                }

                // Native winsqlite3 can hold exclusive file access. Hash before
                // and after only when every native handle has closed.
                string before = Sha256(path);
                List<SyntheticTrackRow> rows;
                using (var catalog = CatalogService.OpenForTesting(path))
                {
                    if (catalog.QuickCheck() != "ok" ||
                        catalog.GetCounts().Tracks != count ||
                        catalog.ForeignKeyViolationCount() != 0)
                        throw new InvalidDataException("Full CatalogService migration/schema validation failed");

                    // OpenForTesting may legitimately migrate a database, but
                    // our fresh v4 test input MUST NOT require any migration.
                    // The final byte hash rejects any unexpected alteration.
                    catalog.EnableQueryOnlyForExperiment();
                    if (!catalog.VerifyWriteIsRejectedForExperiment())
                        throw new InvalidOperationException("Full CatalogService query_only allowed a write");

                    string contract = CatalogService.ValidateNativeProjectionContract(catalog);
                    if (!contract.Contains("/" + count.ToString(CultureInfo.InvariantCulture)))
                        throw new InvalidOperationException("Full CatalogService native projection count failed");
                    var native = catalog.GetNativeTracks();
                    if (native.Count != count ||
                        native[0].Id != 1 || native[^1].Id != count ||
                        native[0].Artist != "Native Artist 001" ||
                        native[^1].Title != "Native Title " + count.ToString("D6", CultureInfo.InvariantCulture))
                        throw new InvalidOperationException("Full CatalogService native SQLite projection mismatch");
                    rows = NativeTrackProjection.Project(native);
                    var selection = SyntheticSelection.Capture(new[] { rows[0], rows[^1] });
                    if (SyntheticSelection.Restore(SyntheticCatalog.SortByTitle(rows), selection).Count != 2)
                        throw new InvalidOperationException("Full CatalogService lost stable catalog IDs");
                }

                if (!string.Equals(before, Sha256(path), StringComparison.Ordinal))
                    throw new InvalidOperationException("Full CatalogService read-only path changed SQLite bytes");
                return rows;
            }
            finally
            {
                // Never traverse outside this generated disposable directory.
                try { Directory.Delete(dir, true); } catch { }
            }
        }
        private static string Sha256(string path)
        {
            using (var hash = SHA256.Create())
            using (var file = File.OpenRead(path))
                return Convert.ToHexString(hash.ComputeHash(file));
        }
    }
}
