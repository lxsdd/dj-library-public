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
                VerifyLiveNormalizedIdentityGate();
                VerifyLiveDigitalLookupIndex();
                VerifyLiveBridgeIdentityIntegrity();
                string scaleReport = VerifyLiveBridgeLargeGeneration();
                string denseReport = VerifyLiveBridgeDenseCatalog();

                if (CdxCompatibility.Classify("150 360150", false) != CdxCompatibilityState.Unknown ||
                    CdxCompatibility.Classify("150 360150 2965", true) != CdxCompatibilityState.Unknown)
                    throw new InvalidDataException("Public-smoke unknown CDX classification failed.");

                return "PASS_PUBLIC_CONTRACT: clean schema-v4 bootstrap, synthetic CRUD, native projection, " +
                       "CDX compatible/incompatible/unknown, SQLite WAL/gzip/base64 and JSON v1 roundtrip, " +
                       "PASS_LIVE_IDENTITY_AND_CUE_PROJECTION, PASS_LIVE_NORMALIZED_IDENTITY_GATE, PASS_LIVE_ITEM_ID_LOOKUP, " +
                       "PASS_LIVE_BRIDGE_ID_INTEGRITY, " + scaleReport + ", " + denseReport + ", no user fixtures";
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

        // Regression: equal empty normalized strings do NOT establish mix/title identity.
        // Positive controls retain meaningful structured and displayed-title matches.
        private static void VerifyLiveNormalizedIdentityGate()
        {
            var store = new DataStore();
            var track = new TrackRow {
                TrackId = 1, DiscId = 1, Artist = "Example Artist",
                Title = "Fictional Track", DisplayTitle = "Different Display",
                Version = "!!!", DurationSeconds = 180,
                LegacyGenre = "Legacy", Genre = "Legacy"
            };
            var disc = new CdRow { DiscId = 1, Tracks = 1, LegacyGenre = "Legacy", Genre = "Legacy" };
            store.Tracks.Add(track);
            store.Cds.Add(disc);
            store.TracksByDisc.Add(1, new System.Collections.Generic.List<TrackRow> { track });
            var item = new DigitalItem {
                ItemId = 101, Subsong = 1, Artist = "Example Artist",
                Title = "Another Display", OriginalTitle = "Fictional Track",
                RemixedBy = "", DurationSeconds = 180, Genre = "Trance"
            };
            var generation = new System.Collections.Generic.List<DigitalItem> { item };

            // Old behavior: Norm("!!!") == Norm("") incorrectly granted STRONG.
            store.ApplyLiveDigitalItems(generation);
            if (track.StrongCount != 0 || track.LikelyCount != 1 ||
                track.Genre != "Legacy" || disc.Genre != "Legacy" ||
                track.GenreProjected || disc.GenreProjected ||
                store.GetMatchesForTrack(1).Count != 1)
                throw new InvalidDataException("Punctuation-only mix identity projected a virtual GENRE.");

            track.Version = "Extended Mix";
            item.RemixedBy = "Extended Mix";
            store.ApplyLiveDigitalItems(generation);
            if (track.StrongCount != 1 || track.Genre != "Trance" ||
                disc.Genre != "Trance" || store.ProjectedTrackGenreCount != 1)
                throw new InvalidDataException("Meaningful structured mix identity regressed.");

            // Old behavior: Norm("---") == Norm("!!!") incorrectly granted STRONG.
            track.Version = "";
            track.DisplayTitle = "---";
            item.Title = "!!!";
            item.RemixedBy = "";
            store.ApplyLiveDigitalItems(generation);
            if (track.StrongCount != 0 || track.LikelyCount != 1 ||
                track.Genre != "Legacy" || disc.Genre != "Legacy" ||
                track.GenreProjected || disc.GenreProjected)
                throw new InvalidDataException("Punctuation-only display identity projected a virtual GENRE.");

            track.DisplayTitle = "Fictional Track";
            item.Title = "Fictional Track";
            store.ApplyLiveDigitalItems(generation);
            if (track.StrongCount != 1 || track.Genre != "Trance" ||
                disc.Genre != "Trance" || store.GetMatchesForTrack(1).Count != 1)
                throw new InvalidDataException("Meaningful displayed-title identity regressed.");
        }

        private static void VerifyLiveDigitalLookupIndex()
        {
            var store = new DataStore();
            if (store.GetActiveDigitalItem(1) != null ||
                store.GetActiveDigitalItem(0) != null)
                throw new InvalidDataException("Uninitialized live lookup returned an item.");

            // Exercise the UI's actual ID lookup with synthetic, high/sparse IDs
            // and a repeated ID, without reading user audio or bridge metadata.
            var first = new DigitalItem { ItemId = 17, Artist = "First" };
            var duplicate = new DigitalItem { ItemId = 17, Artist = "Duplicate" };
            var last = new DigitalItem { ItemId = 50000, Artist = "Last" };
            var generation = new System.Collections.Generic.List<DigitalItem> {
                first, duplicate, new DigitalItem { ItemId = 0 }, last
            };
            store.ApplyLiveDigitalItems(generation);
            generation.Clear(); // The store owns the generation's list snapshot.
            if (!Object.ReferenceEquals(store.GetActiveDigitalItem(17), first) ||
                !Object.ReferenceEquals(store.GetActiveDigitalItem(50000), last) ||
                store.GetActiveDigitalItem(0) != null ||
                store.GetActiveDigitalItem(-2) != null ||
                store.GetActiveDigitalItem(49999) != null)
                throw new InvalidDataException("Live index changed lookup/duplicate-ID behavior.");

            var replacement = new DigitalItem { ItemId = 17, Artist = "New generation" };
            store.ApplyLiveDigitalItems(new System.Collections.Generic.List<DigitalItem> { replacement });
            if (!Object.ReferenceEquals(store.GetActiveDigitalItem(17), replacement) ||
                store.GetActiveDigitalItem(50000) != null)
                throw new InvalidDataException("Live index retained a stale Bridge generation.");

            store.ApplyLiveDigitalItems(new System.Collections.Generic.List<DigitalItem>());
            if (store.GetActiveDigitalItem(17) != null ||
                store.GetActiveDigitalItem(50000) != null)
                throw new InvalidDataException("Empty Bridge generation retained stale lookups.");
        }


        // Source-only capacity regression modeled on the real 54k+ foobar Bridge.
        // No owner paths, media, profile or database; all entities are synthetic.
        // Durations are diagnostics, not a hard wall-clock PASS threshold on shared CI.
        private static string VerifyLiveBridgeLargeGeneration()
        {
            const int itemCount = 54129;
            const int lookupProbes = 40000;
            var store = new DataStore();
            var tracks = new System.Collections.Generic.List<TrackRow>();
            var disc = new CdRow { DiscId = 1, Tracks = 3, LegacyGenre = "Legacy", Genre = "Legacy" };
            store.Cds.Add(disc);
            int[] targets = { 1, 27065, itemCount };
            for (int i = 0; i < targets.Length; i++)
            {
                int id = targets[i];
                var track = new TrackRow {
                    TrackId = i + 1, DiscId = 1, Artist = "Synthetic Artist",
                    Title = "Synthetic Track " + id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    DurationSeconds = 180, LegacyGenre = "Legacy", Genre = "Legacy"
                };
                track.DisplayTitle = track.Title;
                tracks.Add(track);
                store.Tracks.Add(track);
            }
            store.TracksByDisc.Add(disc.DiscId, tracks);
            var generation = new System.Collections.Generic.List<DigitalItem>(itemCount);
            for (int id = 1; id <= itemCount; id++)
            {
                string label = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                generation.Add(new DigitalItem {
                    ItemId = id, Path = @"X:\Synthetic\Bridge\" + label + ".flac",
                    Subsong = id == 27065 ? 1 : 0,
                    Artist = "Synthetic Artist", Title = "Synthetic Track " + label,
                    DurationSeconds = 180,
                    Genre = id == 1 || id == 27065 || id == itemCount ? "Trance" : "House"
                });
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            store.ApplyLiveDigitalItems(generation);
            long applyMs = watch.ElapsedMilliseconds;
            if (store.DigitalItemCount != itemCount ||
                store.ProjectedTrackGenreCount != 3 ||
                store.ProjectedCdGenreCount != 1 ||
                disc.Genre != "Trance")
                throw new InvalidDataException("54k synthetic Bridge generation lost matched tracks or genre consensus.");
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].DigitalCount != 1 || tracks[i].StrongCount != 1 ||
                    tracks[i].Genre != "Trance" || store.GetMatchesForTrack(tracks[i].TrackId).Count != 1)
                    throw new InvalidDataException("54k synthetic Bridge lookup changed the real matching path.");
            }

            watch.Restart();
            for (int i = 0; i < lookupProbes; i++)
            {
                int id = 1 + (i * 7919 % itemCount);
                if (!Object.ReferenceEquals(store.GetActiveDigitalItem(id), generation[id - 1]))
                    throw new InvalidDataException("54k synthetic Bridge indexed ID lookup returned the wrong row.");
            }
            long lookupMs = watch.ElapsedMilliseconds;
            if (store.GetActiveDigitalItem(0) != null ||
                store.GetActiveDigitalItem(itemCount + 1) != null)
                throw new InvalidDataException("54k Bridge indexed lookup accepted an invalid ID.");

            // A replacement generation must retire all 54k previous row identities
            // AND all derived match/genre/search state, with no stale strong vote.
            var newItem = new DigitalItem {
                ItemId = 27065, Artist = "Different Artist", Title = "Different Track",
                Genre = "Techno", Subsong = 0
            };
            store.ApplyLiveDigitalItems(new System.Collections.Generic.List<DigitalItem> { newItem });
            if (store.DigitalItemCount != 1 ||
                !Object.ReferenceEquals(store.GetActiveDigitalItem(27065), newItem) ||
                store.GetActiveDigitalItem(1) != null ||
                store.GetActiveDigitalItem(itemCount) != null ||
                store.ProjectedTrackGenreCount != 0 ||
                store.ProjectedCdGenreCount != 0 || disc.Genre != "Legacy")
                throw new InvalidDataException("54k Bridge generation replacement retained stale identities or genre.");
            foreach (TrackRow t in tracks)
            {
                if (t.DigitalCount != 0 || t.Genre != "Legacy" ||
                    store.GetMatchesForTrack(t.TrackId).Count != 0 ||
                    t.SearchText.Contains("trance"))
                    throw new InvalidDataException("54k Bridge replacement retained stale digital match/search state.");
            }
            return "PASS_LIVE_BRIDGE_54K_SCALE(items=" + itemCount +
                ";lookups=" + lookupProbes + ";apply_ms=" + applyMs +
                ";lookup_ms=" + lookupMs + ")";
        }


        // Synthetic stress of dense CD-track matching, not only 54k sparse ID lookup.
        // Each of 12 CDs contributes 1,000 real match candidates; no user fixtures.
        private static string VerifyLiveBridgeDenseCatalog()
        {
            const int discCount = 12;
            const int tracksPerDisc = 1000;
            const int catalogTrackCount = discCount * tracksPerDisc;
            const int bridgeItemCount = 54129;
            var store = new DataStore();
            var tracks = new System.Collections.Generic.List<TrackRow>(catalogTrackCount);
            string[] genres = { "Trance", "House", "Techno" };

            for (int discId = 1; discId <= discCount; discId++)
            {
                var disc = new CdRow {
                    DiscId = discId, Tracks = tracksPerDisc,
                    LegacyGenre = "Legacy", Genre = "Legacy"
                };
                store.Cds.Add(disc);
                var discTracks = new System.Collections.Generic.List<TrackRow>(tracksPerDisc);
                for (int position = 1; position <= tracksPerDisc; position++)
                {
                    int id = (discId - 1) * tracksPerDisc + position;
                    string label = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var track = new TrackRow {
                        TrackId = id, DiscId = discId,
                        Artist = "Synthetic CD Artist", Title = "Dense Track " + label,
                        DisplayTitle = "Dense Track " + label,
                        DurationSeconds = 180, LegacyGenre = "Legacy", Genre = "Legacy"
                    };
                    tracks.Add(track);
                    discTracks.Add(track);
                    store.Tracks.Add(track);
                }
                store.TracksByDisc.Add(discId, discTracks);
            }

            var bridge = new System.Collections.Generic.List<DigitalItem>(bridgeItemCount);
            for (int id = 1; id <= bridgeItemCount; id++)
            {
                string label = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                bool inCatalog = id <= catalogTrackCount;
                bridge.Add(new DigitalItem {
                    ItemId = id, Path = @"X:\Synthetic\Dense\" + label + ".flac",
                    Subsong = inCatalog && id % 17 == 0 ? 1 : 0,
                    Artist = inCatalog ? "Synthetic CD Artist" : "Unrelated Bridge Artist",
                    Title = inCatalog ? "Dense Track " + label : "Noise " + label,
                    DurationSeconds = 180,
                    Genre = inCatalog ? genres[(id - 1) / tracksPerDisc % genres.Length] : "Other"
                });
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            store.ApplyLiveDigitalItems(bridge);
            long applyMs = watch.ElapsedMilliseconds;
            if (store.DigitalItemCount != bridgeItemCount ||
                store.ProjectedTrackGenreCount != catalogTrackCount ||
                store.ProjectedCdGenreCount != discCount)
                throw new InvalidDataException("Dense synthetic CD catalog lost matches or genre projections.");

            foreach (TrackRow track in tracks)
            {
                var matches = store.GetMatchesForTrack(track.TrackId);
                string expectedGenre = genres[(track.DiscId - 1) % genres.Length];
                if (track.DigitalCount != 1 || track.StrongCount != 1 ||
                    track.LikelyCount != 0 || track.CandidateCount != 0 ||
                    matches.Count != 1 || matches[0].DigitalItemId != track.TrackId ||
                    track.Genre != expectedGenre || !track.GenreProjected ||
                    matches[0].Subsong != (track.TrackId % 17 == 0 ? 1 : 0))
                    throw new InvalidDataException("Dense synthetic CD matched the wrong Bridge source.");
            }
            foreach (CdRow disc in store.Cds)
            {
                string expectedGenre = genres[(disc.DiscId - 1) % genres.Length];
                if (disc.Genre != expectedGenre || !disc.GenreProjected ||
                    disc.ProjectedGenreTrackCount != tracksPerDisc ||
                    disc.StrongTracks != tracksPerDisc ||
                    disc.GenreCoverage != 1.0 || disc.GenreDominance != 1.0)
                    throw new InvalidDataException("Dense synthetic CD genre consensus changed.");
            }

            // A new Bridge generation must revoke all 12,000 derived matches and
            // all CD projections, not just the 54,129 source ID lookup entries.
            var replacement = new DigitalItem {
                ItemId = 1, Artist = "Other Artist", Title = "Unrelated Track",
                DurationSeconds = 180, Genre = "Other"
            };
            watch.Restart();
            store.ApplyLiveDigitalItems(new System.Collections.Generic.List<DigitalItem> { replacement });
            long clearMs = watch.ElapsedMilliseconds;
            if (store.DigitalItemCount != 1 ||
                store.ProjectedTrackGenreCount != 0 ||
                store.ProjectedCdGenreCount != 0 ||
                !Object.ReferenceEquals(store.GetActiveDigitalItem(1), replacement) ||
                store.GetActiveDigitalItem(bridgeItemCount) != null)
                throw new InvalidDataException("Dense synthetic Bridge replacement retained stale source IDs.");
            foreach (TrackRow track in tracks)
            {
                if (track.DigitalCount != 0 || track.Genre != "Legacy" ||
                    track.GenreProjected || store.GetMatchesForTrack(track.TrackId).Count != 0 ||
                    track.SearchText.Contains("trance") || track.SearchText.Contains("techno"))
                    throw new InvalidDataException("Dense synthetic Bridge replacement retained stale matches.");
            }
            foreach (CdRow disc in store.Cds)
            {
                if (disc.Genre != "Legacy" || disc.GenreProjected ||
                    disc.ProjectedGenreTrackCount != 0 || disc.StrongTracks != 0)
                    throw new InvalidDataException("Dense synthetic Bridge replacement retained stale CD consensus.");
            }
            return "PASS_LIVE_BRIDGE_DENSE_12K_CATALOG(tracks=" + catalogTrackCount +
                ";items=" + bridgeItemCount + ";discs=" + discCount +
                ";apply_ms=" + applyMs + ";clear_ms=" + clearMs + ")";
        }

        private static void VerifyLiveBridgeIdentityIntegrity()
        {
            var store = new DataStore();
            var track = new TrackRow {
                TrackId = 1, DiscId = 1, Artist = "Example Artist",
                Title = "Fictional Track", DisplayTitle = "Fictional Track",
                DurationSeconds = 180, LegacyGenre = "Legacy", Genre = "Legacy"
            };
            var disc = new CdRow { DiscId = 1, Tracks = 1, LegacyGenre = "Legacy", Genre = "Legacy" };
            store.Tracks.Add(track);
            store.Cds.Add(disc);
            store.TracksByDisc.Add(1, new System.Collections.Generic.List<TrackRow> { track });

            var valid = new DigitalItem {
                ItemId = 31, Artist = "Example Artist", Title = "Fictional Track",
                DurationSeconds = 180, Subsong = 1, Genre = "Trance"
            };
            store.ApplyLiveDigitalItems(new System.Collections.Generic.List<DigitalItem> { valid });
            if (track.Genre != "Trance" || disc.Genre != "Trance" ||
                store.GetMatchesForTrack(1).Count != 1)
                throw new InvalidDataException("Valid Bridge baseline did not project.");

            // An invalid generation must not destroy a previously accepted one.
            bool rejectedNull = false;
            try {
                store.ApplyLiveDigitalItems(new System.Collections.Generic.List<DigitalItem> {
                    new DigitalItem { ItemId = 55 }, null
                });
            }
            catch (InvalidDataException) { rejectedNull = true; }
            if (!rejectedNull || !Object.ReferenceEquals(store.GetActiveDigitalItem(31), valid) ||
                track.Genre != "Trance" || disc.Genre != "Trance" ||
                store.DigitalItemCount != 1 || store.GetMatchesForTrack(1).Count != 1)
                throw new InvalidDataException("Malformed Bridge generation corrupted live state.");

            // Rows without selectable IDs must never grant GENRE evidence.
            store.ApplyLiveDigitalItems(new System.Collections.Generic.List<DigitalItem> {
                new DigitalItem { ItemId = 0, Artist = "Example Artist",
                    Title = "Fictional Track", Genre = "Wrong" },
                new DigitalItem { ItemId = -7, Artist = "Example Artist",
                    Title = "Fictional Track", Genre = "Wrong" }
            });
            if (track.DigitalCount != 0 || track.Genre != "Legacy" ||
                disc.Genre != "Legacy" || store.GetMatchesForTrack(1).Count != 0)
                throw new InvalidDataException("Non-positive Bridge IDs produced phantom matches.");

            // The second occurrence of an ID cannot match when the first
            // (the only row returned by GetActiveDigitalItem) does not match.
            var firstOther = new DigitalItem {
                ItemId = 31, Artist = "Other", Title = "Other", Genre = "Wrong"
            };
            var laterMatching = new DigitalItem {
                ItemId = 31, Artist = "Example Artist", Title = "Fictional Track",
                DurationSeconds = 180, Subsong = 1, Genre = "Wrong"
            };
            store.ApplyLiveDigitalItems(new System.Collections.Generic.List<DigitalItem> {
                firstOther, laterMatching
            });
            if (!Object.ReferenceEquals(store.GetActiveDigitalItem(31), firstOther) ||
                track.DigitalCount != 0 || track.Genre != "Legacy" ||
                store.GetMatchesForTrack(1).Count != 0)
                throw new InvalidDataException("Duplicate Bridge ID matched a hidden later row.");

            // Conversely, an eligible first row must remain the sole source of
            // confidence and genre, regardless of a later conflicting duplicate.
            var conflicting = new DigitalItem {
                ItemId = 31, Artist = "Example Artist", Title = "Fictional Track",
                DurationSeconds = 180, Subsong = 1, Genre = "Techno"
            };
            store.ApplyLiveDigitalItems(new System.Collections.Generic.List<DigitalItem> {
                valid, conflicting
            });
            if (!Object.ReferenceEquals(store.GetActiveDigitalItem(31), valid) ||
                track.DigitalCount != 1 || track.StrongCount != 1 ||
                track.Genre != "Trance" || track.GenreEvidenceCount != 1 ||
                disc.Genre != "Trance" || store.GetMatchesForTrack(1).Count != 1 ||
                store.GetMatchesForTrack(1)[0].Genre != "Trance")
                throw new InvalidDataException("Duplicate Bridge ID distorted first-row projection.");
        }

        private static void Verify(CatalogCounts counts, long releases, long discs, long tracks, string stage)
        {
            if (counts.Releases != releases || counts.Discs != discs || counts.Tracks != tracks)
                throw new InvalidDataException("Public-smoke counts differ: " + stage);
        }
    }
}
