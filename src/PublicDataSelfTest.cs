using System;
using System.IO;

namespace DJLibrary
{
    // A public-safe smoke test: no music files, user app-data, bridge profile
    // or historical catalog fixture may be consulted by this contract.
    internal static class PublicDataSelfTest
    {
        internal static string Run()
        {
            string dir = Path.Combine(Path.GetTempPath(), "DJLibrary-public-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "synthetic.sqlite");
                string exported = Path.Combine(dir, "local-export.sqlite");
                using (CatalogService catalog = CatalogService.CreateEmptyForTesting(path))
                {
                    Verify(catalog.GetCounts(), 0, 0, 0, "clean first launch");
                    long release = catalog.CreateRelease(new CatalogRelease {
                        AlbumArtist = "Example Artist", Album = "Synthetic Album",
                        Genre = "Test", TotalDiscs = 2
                    });
                    long cd1 = catalog.AddDisc(new CatalogDisc {
                        ReleaseId = release, DiscNumber = 1, Medium = "CD",
                        Toc = "150 360148", TocComplete = true
                    });
                    long cd2 = catalog.AddDisc(new CatalogDisc {
                        ReleaseId = release, DiscNumber = 2, Medium = "CD",
                        Toc = "150 360149", TocComplete = true
                    });
                    catalog.AddTrack(new CatalogTrack {
                        DiscId = cd1, Position = 1, Artist = "Example Artist", Title = "Fictional Track A", Bpm = 120
                    });
                    catalog.AddTrack(new CatalogTrack {
                        DiscId = cd2, Position = 1, Artist = "Example Artist", Title = "Fictional Track B", Bpm = 90
                    });
                    Verify(catalog.GetCounts(), 1, 2, 2, "synthetic import/CRUD");
                    if (catalog.ForeignKeyViolationCount() != 0)
                        throw new InvalidDataException("Public-smoke foreign key check failed.");
                    CatalogService.ValidateNativeProjectionContract(catalog);
                    DataStore.ValidateCatalogSourceContract(catalog, null);
                    if (catalog.GetDisc(cd1).CdxCompatibilityState != CdxCompatibilityState.Compatible ||
                        catalog.GetDisc(cd2).CdxCompatibilityState != CdxCompatibilityState.Incompatible)
                        throw new InvalidDataException("Public-smoke CDX boundary classification failed.");
                    catalog.Backup(exported);
                    catalog.CreateRelease(new CatalogRelease { AlbumArtist = "Temporary", Album = "Not exported" });
                    Verify(catalog.GetCounts(), 2, 2, 2, "pre-restore change");

                    // A syntactically valid SQLite file with an invalid DJ Library
                    // schema must never replace the live user catalog.
                    string invalid = Path.Combine(dir, "not-a-dj-library.sqlite");
                    using (WinSqliteDb foreignDb = new WinSqliteDb(invalid))
                        foreignDb.Execute("CREATE TABLE unrelated(id INTEGER PRIMARY KEY)");
                    bool rejected = false;
                    try { catalog.Restore(invalid); }
                    catch (InvalidOperationException) { rejected = true; }
                    catch (InvalidDataException) { rejected = true; }
                    if (!rejected) throw new InvalidDataException("Invalid local import was accepted.");
                    Verify(catalog.GetCounts(), 2, 2, 2, "invalid import preserved current catalog");

                    catalog.Restore(exported);
                    Verify(catalog.GetCounts(), 1, 2, 2, "local import/restore");
                }
                if (CdxCompatibility.Classify("150 360150", false) != CdxCompatibilityState.Unknown ||
                    CdxCompatibility.Classify("150 360150 2965", true) != CdxCompatibilityState.Unknown)
                    throw new InvalidDataException("Public-smoke unknown CDX classification failed.");

                return "PASS_PUBLIC_CONTRACT: clean schema-v4 bootstrap, synthetic CRUD, native projection, " +
                       "CDX compatible/incompatible/unknown, local SQLite export/import, no user fixtures";
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        private static void Verify(CatalogCounts counts, long releases, long discs, long tracks, string stage)
        {
            if (counts.Releases != releases || counts.Discs != discs || counts.Tracks != tracks)
                throw new InvalidDataException("Public-smoke counts differ: " + stage);
        }
    }
}
