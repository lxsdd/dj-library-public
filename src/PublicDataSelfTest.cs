using System;
using System.IO;
using System.IO.Compression;

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

                    // WAL fixture: leave another SQLite connection open while
                    // importing. The latest committed release can live in -wal;
                    // a raw file copy of .sqlite would silently discard it.
                    string walSource = Path.Combine(dir, "wal-source.sqlite");
                    catalog.Backup(walSource);
                    using (WinSqliteDb walWriter = new WinSqliteDb(walSource))
                    {
                        walWriter.Execute("PRAGMA journal_mode=WAL");
                        walWriter.Execute("INSERT INTO release(album_artist,album) VALUES('WAL Artist','WAL Commit')");
                        catalog.ImportCatalogFile(walSource);
                        Verify(catalog.GetCounts(), 3, 2, 2, "open-WAL snapshot import");
                    }

                    // Supported compression formats must use the same safe
                    // preflight, even for the oldest locally held backups.
                    string gz = Path.Combine(dir, "compressed.sqlite.gz");
                    using (FileStream output = File.Create(gz))
                    using (GZipStream zipper = new GZipStream(output, CompressionMode.Compress))
                    using (FileStream input = File.OpenRead(exported))
                        input.CopyTo(zipper);
                    catalog.ImportCatalogFile(gz);
                    Verify(catalog.GetCounts(), 1, 2, 2, "gzip SQLite import");

                    string b64 = Path.Combine(dir, "compressed.sqlite.gz.b64");
                    File.WriteAllText(b64, Convert.ToBase64String(File.ReadAllBytes(gz)));
                    catalog.ImportCatalogFile(b64);
                    Verify(catalog.GetCounts(), 1, 2, 2, "base64+gzip SQLite import");

                    catalog.Restore(exported);
                    Verify(catalog.GetCounts(), 1, 2, 2, "local import/restore");

                    // Vendor-neutral metadata interchange: all identifiers are
                    // re-created in an isolated staging DB before replacement.
                    string interchange = Path.Combine(dir, "portable-catalog.json");
                    catalog.ExportJsonInterchange(interchange);
                    catalog.CreateRelease(new CatalogRelease {
                        AlbumArtist = "Temporary", Album = "Must not survive JSON import"
                    });
                    Verify(catalog.GetCounts(), 2, 2, 2, "pre-interchange mutation");
                    catalog.ImportJsonInterchange(interchange);
                    Verify(catalog.GetCounts(), 1, 2, 2, "JSON export/import roundtrip");
                    if (catalog.ForeignKeyViolationCount() != 0 ||
                        catalog.GetDiscs(catalog.GetReleases()[0].Id)[0].CdxCompatibilityState != CdxCompatibilityState.Compatible)
                        throw new InvalidDataException("JSON interchange lost physical CDX/TOC data.");

                    string wrongFormat = Path.Combine(dir, "not-interchange.json");
                    File.WriteAllText(wrongFormat, "{}");
                    bool rejectedJson = false;
                    try { catalog.ImportJsonInterchange(wrongFormat); }
                    catch (InvalidDataException) { rejectedJson = true; }
                    if (!rejectedJson) throw new InvalidDataException("Foreign JSON format was accepted.");
                    Verify(catalog.GetCounts(), 1, 2, 2, "invalid JSON preserves live catalog");
                }
                // Virtual foobar CUE/subsong rows must remain searchable, yet
                // must never be promoted to "strong" without a verified
                // segment-vs-CD duration. This is a synthetic, no-media test.
                var physical = new DigitalItem { Path = @"X:\\Music\\Mix.flac", Subsong = 0 };
                var virtualOne = new DigitalItem { Path = physical.Path, Subsong = 1 };
                var virtualTwo = new DigitalItem { Path = physical.Path, Subsong = 2 };
                if (physical.IsVirtualSubsong || !virtualOne.IsVirtualSubsong ||
                    !virtualTwo.SourceKindText.Contains("#2") ||
                    !(new MatchRow { Path = virtualOne.Path, Subsong = 1 }).IsVirtualSubsong)
                    throw new InvalidDataException("Digital match source-kind labels lost path+subsong provenance.");

                if (DataStore.QualifiesStrongDigitalMatch(true, false, false, 1) ||
                    !DataStore.QualifiesStrongDigitalMatch(true, false, true, 1) ||
                    DataStore.QualifiesStrongDigitalMatch(true, true, false, 1) ||
                    !DataStore.QualifiesStrongDigitalMatch(true, false, false, 0) ||
                    DataStore.QualifiesStrongDigitalMatch(false, false, true, 1))
                    throw new InvalidDataException("Virtual CUE duration did not gate strong matching.");

                if (DataStore.MayProjectGenreFromStrongMatches(new System.Collections.Generic.List<DigitalItem> { virtualOne, virtualTwo }) ||
                    !DataStore.MayProjectGenreFromStrongMatches(new System.Collections.Generic.List<DigitalItem> { virtualOne }) ||
                    !DataStore.MayProjectGenreFromStrongMatches(new System.Collections.Generic.List<DigitalItem> { physical, new DigitalItem { Subsong = 0 } }))
                    throw new InvalidDataException("Virtual CUE match ambiguity entered automatic genre projection.");

                // Exercise the real live matcher and CD projection, not only its
                // helper predicates. All rows below exist only in memory.
                VerifyLiveIdentityAndCueProjection();

                if (CdxCompatibility.Classify("150 360150", false) != CdxCompatibilityState.Unknown ||
                    CdxCompatibility.Classify("150 360150 2965", true) != CdxCompatibilityState.Unknown)
                    throw new InvalidDataException("Public-smoke unknown CDX classification failed.");

                return "PASS_PUBLIC_CONTRACT: clean schema-v4 bootstrap, synthetic CRUD, native projection, " +
                       "CDX compatible/incompatible/unknown, SQLite WAL/gzip/base64 and JSON v1 roundtrip, " +
                       "PASS_LIVE_IDENTITY_AND_CUE_PROJECTION, no user fixtures";
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        private static void VerifyLiveIdentityAndCueProjection()
        {
            var store = new DataStore();
            var track = new TrackRow {
                TrackId = 1, DiscId = 1, Artist = "Example Artist",
                Title = "Fictional Cue Track", DisplayTitle = "Fictional Cue Track",
                DurationSeconds = 180, LegacyGenre = "Legacy", Genre = "Legacy"
            };
            var disc = new CdRow { DiscId = 1, Tracks = 1, LegacyGenre = "Legacy", Genre = "Legacy" };
            store.Tracks.Add(track);
            store.Cds.Add(disc);
            store.TracksByDisc.Add(1, new System.Collections.Generic.List<TrackRow> { track });

            var source = new DigitalItem {
                ItemId = 11, Path = @"X:\Synthetic\Fictional.flac", Subsong = 1,
                Artist = "Example Artist", Title = "Fictional Cue Track",
                DurationSeconds = 180, Genre = "Trance"
            };
            var one = new System.Collections.Generic.List<DigitalItem> { source };
            store.ApplyLiveDigitalItems(one);
            if (track.StrongCount != 1 || track.Genre != "Trance" ||
                disc.Genre != "Trance" || store.ProjectedTrackGenreCount != 1 ||
                store.ProjectedCdGenreCount != 1 ||
                store.GetMatchesForTrack(1).Count != 1 ||
                store.GetMatchesForTrack(1)[0].Subsong != 1)
                throw new InvalidDataException("Known-duration virtual match failed production projection.");

            // Same CUE row, unknown duration: retain likely evidence but do not
            // carry over a projected genre or stale search text.
            source.DurationSeconds = 0;
            store.ApplyLiveDigitalItems(one);
            if (track.StrongCount != 0 || track.LikelyCount != 1 ||
                track.Genre != "Legacy" || disc.Genre != "Legacy" ||
                track.SearchText.Contains("trance") || store.ProjectedTrackGenreCount != 0)
                throw new InvalidDataException("Unknown-duration virtual match projected a stale genre.");

            // The old index accepted separator-only artist/title keys and could
            // turn a missing Bridge identity into a strong virtual genre vote.
            source.DurationSeconds = 180;
            source.Artist = "";
            track.Artist = "";
            store.ApplyLiveDigitalItems(one);
            if (track.DigitalCount != 0 || track.GenreProjected ||
                disc.GenreProjected || store.GetMatchesForTrack(1).Count != 0)
                throw new InvalidDataException("Two missing artists were treated as exact identity.");

            track.Artist = "Example Artist";
            source.Artist = "Example Artist";
            source.Title = "";
            track.Title = "";
            track.DisplayTitle = "";
            store.ApplyLiveDigitalItems(one);
            if (track.DigitalCount != 0 || track.GenreProjected)
                throw new InvalidDataException("Two missing titles were treated as exact identity.");
            track.Title = "Fictional Cue Track";
            track.DisplayTitle = "Fictional Cue Track";

            source.Title = "Fictional Cue Track";
            source.Artist = "!!!";
            track.Artist = "!!!";
            store.ApplyLiveDigitalItems(one);
            if (track.DigitalCount != 0 || track.GenreProjected)
                throw new InvalidDataException("Punctuation-only artists were treated as exact identity.");

            track.Artist = "Example Artist";
            source.Artist = "Example Artist";
            source.Title = "---";
            track.Title = "---";
            track.DisplayTitle = "---";
            store.ApplyLiveDigitalItems(one);
            if (track.DigitalCount != 0 || track.GenreProjected)
                throw new InvalidDataException("Punctuation-only titles were treated as exact identity.");
            track.Title = "Fictional Cue Track";
            track.DisplayTitle = "Fictional Cue Track";

            // Preserve the existing physical-source policy: exact identity can
            // still be strong when physical duration is unavailable.
            source.Title = "Fictional Cue Track";
            source.Subsong = 0;
            source.DurationSeconds = 0;
            store.ApplyLiveDigitalItems(one);
            if (track.StrongCount != 1 || track.Genre != "Trance" ||
                disc.Genre != "Trance" || store.GetMatchesForTrack(1)[0].Subsong != 0)
                throw new InvalidDataException("Physical exact-identity baseline regressed.");

            // Two separate strong virtual segments cannot be counted as
            // independent evidence for automatic track/CD genre projection.
            source.Subsong = 1;
            source.DurationSeconds = 180;
            var second = new DigitalItem {
                ItemId = 12, Path = source.Path, Subsong = 2,
                Artist = source.Artist, Title = source.Title,
                DurationSeconds = 180, Genre = "Trance"
            };
            store.ApplyLiveDigitalItems(new System.Collections.Generic.List<DigitalItem> { source, second });
            if (track.StrongCount != 2 || track.Genre != "Legacy" ||
                disc.Genre != "Legacy" || store.ProjectedTrackGenreCount != 0 ||
                store.GetMatchesForTrack(1).Count != 2)
                throw new InvalidDataException("Competing virtual segments projected genre.");
        }

        private static void Verify(CatalogCounts counts, long releases, long discs, long tracks, string stage)
        {
            if (counts.Releases != releases || counts.Discs != discs || counts.Tracks != tracks)
                throw new InvalidDataException("Public-smoke counts differ: " + stage);
        }
    }
}
