using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DJLibrary;

namespace DJLibrary
{
    // TEST ONLY: minimal host for the ACTUAL production CatalogService.NativeProjection.cs.
    // It replaces only the wider CatalogService bootstrap/metadata editors, not its
    // GetNativeTracks SQL, result projection, or original WinSqliteDb implementation.
    // This is NOT a full product CatalogService startup/DB migration test.
    public sealed partial class CatalogService
    {
        private readonly WinSqliteDb _db;
        internal CatalogService(WinSqliteDb db) { _db = db; }
        private static string ToStringValue(object value) =>
            value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
        private static long ToLong(object value) =>
            value == null ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        private static double ToDouble(object value) =>
            value == null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        public CatalogCounts GetCounts() => new CatalogCounts
        {
            Releases = ToLong(_db.Scalar("SELECT COUNT(*) FROM release")),
            Discs = ToLong(_db.Scalar("SELECT COUNT(*) FROM disc")),
            Tracks = ToLong(_db.Scalar("SELECT COUNT(*) FROM track"))
        };
    }
    public sealed class CatalogCounts
    {
        public long Releases { get; set; }
        public long Discs { get; set; }
        public long Tracks { get; set; }
    }
}

namespace DJLibrary.SyntheticComparison
{
    public static class NativeSqliteFixture
    {
        // Only newly created disposable temporary SQLite databases are touched.
        // No real catalog path, app configuration, foobar profile, bridge or
        // user-controlled file path can be passed to this entry point.
        public static List<SyntheticTrackRow> LoadReadOnlySqlite(int count)
        {
            if (count < 1 || count > 15000)
                throw new ArgumentOutOfRangeException(nameof(count));
            string dir = Path.Combine(Path.GetTempPath(),
                "DJLibrary-WinUi-Sqlite-A-B-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "synthetic-native.sqlite");
            try
            {
                using (var db = new WinSqliteDb(path))
                {
                    db.Execute("PRAGMA foreign_keys=ON");
                    // Exact current catalog-v4 release/disc/track table definitions
                    // from production CatalogService.InstallEmpty, with only these
                    // three tables materialized for the read-only SELECT contract.
                    db.Execute("CREATE TABLE release(id INTEGER PRIMARY KEY,album_artist TEXT NOT NULL DEFAULT '',album TEXT NOT NULL DEFAULT '',release_date TEXT NOT NULL DEFAULT '',genre TEXT NOT NULL DEFAULT '',label TEXT NOT NULL DEFAULT '',catalog TEXT NOT NULL DEFAULT '',country TEXT NOT NULL DEFAULT '',total_discs INTEGER NOT NULL DEFAULT 1 CHECK(total_discs>0))");
                    db.Execute("CREATE TABLE disc(id INTEGER PRIMARY KEY,release_id INTEGER NOT NULL REFERENCES release(id) ON DELETE CASCADE,legacy_album_id INTEGER,disc_number INTEGER NOT NULL DEFAULT 1 CHECK(disc_number>0),medium TEXT NOT NULL DEFAULT '',toc TEXT NOT NULL DEFAULT '',toc_complete INTEGER NOT NULL DEFAULT 0 CHECK(toc_complete IN(0,1)),cd_text_present INTEGER NOT NULL DEFAULT 0 CHECK(cd_text_present IN(0,1)),duration_seconds REAL NOT NULL DEFAULT 0 CHECK(duration_seconds>=0),legacy_json TEXT NOT NULL DEFAULT '{}',cd_text_status TEXT NOT NULL DEFAULT 'legacy_unknown')");
                    db.Execute("CREATE TABLE track(id INTEGER PRIMARY KEY,disc_id INTEGER NOT NULL REFERENCES disc(id) ON DELETE CASCADE,position INTEGER NOT NULL CHECK(position>0),artist TEXT NOT NULL DEFAULT '',title TEXT NOT NULL DEFAULT '',version TEXT NOT NULL DEFAULT '',release_date TEXT NOT NULL DEFAULT '',genre TEXT NOT NULL DEFAULT '',legacy_genre TEXT NOT NULL DEFAULT '',bpm REAL NOT NULL DEFAULT 0 CHECK(bpm>=0),duration_seconds REAL NOT NULL DEFAULT 0 CHECK(duration_seconds>=0),legacy_artist_raw TEXT NOT NULL DEFAULT '',legacy_json TEXT NOT NULL DEFAULT '{}',UNIQUE(disc_id,position))");
                    db.Transaction(() =>
                    {
                        db.Execute("INSERT INTO release(id,album_artist,album,total_discs) VALUES(1,'Synthetic DJ','Synthetic Readonly SQLite',1)");
                        int discCount = (count + 11) / 12;
                        for (int d = 1; d <= discCount; d++)
                            db.Execute("INSERT INTO disc(id,release_id,disc_number,medium) VALUES(?,1,1,'Digital')", d);
                        for (int i = 1; i <= count; i++)
                        {
                            int disc = ((i - 1) / 12) + 1;
                            int ordinal = ((i - 1) % 12) + 1;
                            db.Execute("INSERT INTO track(id,disc_id,position,artist,title,release_date,genre,duration_seconds) VALUES(?,?,?,?,?,?,?,?)",
                                i, disc, ordinal,
                                "Native Artist " + (i % 131).ToString("D3", CultureInfo.InvariantCulture),
                                "Native Title " + i.ToString("D6", CultureInfo.InvariantCulture),
                                "2026-10-19", i % 2 == 0 ? "Trance" : "House", 180 + i % 70);
                        }
                    });
                    if (db.QuickCheck() != "ok")
                        throw new InvalidDataException("Disposable SQLite fixture failed quick_check");
                    string before = Sha256(path);
                    // Fail-closed read-only mode BEFORE real CatalogService SELECT.
                    db.Execute("PRAGMA query_only=ON");
                    if (ToInt(db.Scalar("PRAGMA query_only")) != 1)
                        throw new InvalidOperationException("SQLite query_only guard not enabled");
                    bool writeRejected = false;
                    try { db.Execute("INSERT INTO release(album_artist,album) VALUES('FORBIDDEN','WRITE')"); }
                    catch (InvalidOperationException) { writeRejected = true; }
                    if (!writeRejected)
                        throw new InvalidOperationException("SQLite query_only connection allowed a write");

                    var catalog = new CatalogService(db);
                    string contract = CatalogService.ValidateNativeProjectionContract(catalog);
                    if (!contract.Contains("/" + count.ToString(CultureInfo.InvariantCulture)))
                        throw new InvalidOperationException("Native SQL projection unexpectedly changed item count");
                    var native = catalog.GetNativeTracks(); // ORIGINAL production SQL and row mapping
                    if (native.Count != count || native[0].Id != 1 || native[^1].Id != count ||
                        native[0].Artist != "Native Artist 001" ||
                        native[^1].Title != "Native Title " + count.ToString("D6", CultureInfo.InvariantCulture))
                        throw new InvalidOperationException("Native SQLite GetNativeTracks source projection mismatch");
                    var rows = NativeTrackProjection.Project(native);
                    if (rows.Count != count || rows[0].Identity != "native-track-000000000001")
                        throw new InvalidOperationException("Native SQLite rows lost track identity");
                    var ids = SyntheticSelection.Capture(new[] { rows[0], rows[^1] });
                    if (SyntheticSelection.Restore(SyntheticCatalog.SortByTitle(rows), ids).Count != 2)
                        throw new InvalidOperationException("Native SQLite identities not stable under sorting");
                    if (!string.Equals(before, Sha256(path), StringComparison.Ordinal))
                        throw new InvalidOperationException("Read-only SQLite test modified the fixture database");
                    return rows;
                }
            }
            finally
            {
                // Only disposable files inside this code-generated private subdir.
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
        private static int ToInt(object value) =>
            Convert.ToInt32(value, CultureInfo.InvariantCulture);
        private static string Sha256(string path)
        {
            using (var hash = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return Convert.ToHexString(hash.ComputeHash(fs));
        }
    }
}
