// Fully disposable, producer-derived release/disc/track navigation test.
// Original CatalogService APIs and native WinSqlite SQL are linked unchanged.
// Never read user profile, bridge, music or an existing SQLite catalog.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DJLibrary;

namespace DJLibrary.SyntheticComparison;

public sealed class CatalogNavigationSnapshot
{
    public List<SyntheticTrackRow> Releases { get; } = new();
    public Dictionary<long, List<SyntheticTrackRow>> Discs { get; } = new();
    public Dictionary<long, List<SyntheticTrackRow>> Tracks { get; } = new();

    public List<SyntheticTrackRow> ForDiscs(long releaseId) =>
        Discs.TryGetValue(releaseId, out var rows) ? rows : new();
    public List<SyntheticTrackRow> ForTracks(long discId) =>
        Tracks.TryGetValue(discId, out var rows) ? rows : new();
}

public static class CatalogNavigationFixture
{
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void Must(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException("Catalog navigation: " + message);
    }

    private static SyntheticTrackRow ReleaseRow(CatalogRelease r) =>
        new("release:" + r.Id, r.AlbumArtist, r.Album, r.Genre, r.ReleaseDate,
            r.TotalDiscs + " disc(s)");

    private static SyntheticTrackRow DiscRow(CatalogRelease r, CatalogDisc d) =>
        new("disc:" + d.Id, r.AlbumArtist, "Disc " + d.DiscNumber,
            d.Medium, d.Toc, d.CdTextStatus);

    private static SyntheticTrackRow TrackRow(CatalogTrack t) =>
        new("track:" + t.Id, t.Artist, t.DisplayTitle,
            t.Genre, t.ReleaseDate, t.DurationText);

    public static CatalogNavigationSnapshot Load()
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "DJLibrary-Navigation-A-B-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "navigation-v4.sqlite");
        try
        {
            using (var schema = CatalogService.CreateEmptyForTesting(path))
                Must(schema.SchemaVersion == 4, "schema bootstrap is not v4");

            using (var writer = new WinSqliteDb(path))
            {
                writer.Execute("PRAGMA foreign_keys=ON");
                writer.Transaction(() =>
                {
                    writer.Execute("INSERT INTO release(id,album_artist,album,total_discs,genre) VALUES(1,'Navigation Artist','First Album',2,'Trance')");
                    writer.Execute("INSERT INTO release(id,album_artist,album,total_discs,genre) VALUES(2,'Navigation Artist','Second Album',1,'House')");
                    writer.Execute("INSERT INTO release(id,album_artist,album,total_discs,genre) VALUES(3,'Navigation Artist','Empty Album',1,'Other')");
                    writer.Execute("INSERT INTO disc(id,release_id,disc_number,medium,toc,toc_complete,cd_text_present,cd_text_status) VALUES(11,1,1,'CD','150 12000 24000',1,1,'drive_present')");
                    writer.Execute("INSERT INTO disc(id,release_id,disc_number,medium,toc,toc_complete,cd_text_status) VALUES(12,1,2,'CD','150 12500 24500',1,'drive_absent')");
                    writer.Execute("INSERT INTO disc(id,release_id,disc_number,medium,cd_text_status) VALUES(21,2,1,'Digital','drive_unavailable')");
                    writer.Execute("INSERT INTO track(id,disc_id,position,artist,title,release_date,genre,duration_seconds) VALUES(101,11,1,'Navigation Artist','Opening','2026-10-19','Trance',189)");
                    writer.Execute("INSERT INTO track(id,disc_id,position,artist,title,release_date,genre,duration_seconds) VALUES(102,11,2,'Navigation Artist','Closing','2026-10-19','Trance',210)");
                    writer.Execute("INSERT INTO track(id,disc_id,position,artist,title,release_date,genre,duration_seconds) VALUES(121,12,1,'Navigation Artist','Encore','2026-10-19','Trance',177)");
                    writer.Execute("INSERT INTO track(id,disc_id,position,artist,title,version,release_date,genre,duration_seconds) VALUES(122,12,2,'Navigation Artist','Bonus','Extended','2026-10-19','Trance',231)");
                    writer.Execute("INSERT INTO track(id,disc_id,position,artist,title,release_date,genre,duration_seconds) VALUES(201,21,1,'Other Artist','Return','2026-10-19','House',205)");
                });
                bool rejectedForeignKey = false, rejectedDuplicatePosition = false;
                try { writer.Execute("INSERT INTO disc(id,release_id,disc_number) VALUES(999,987654,1)"); }
                catch (InvalidOperationException) { rejectedForeignKey = true; }
                try { writer.Execute("INSERT INTO track(id,disc_id,position) VALUES(999,11,1)"); }
                catch (InvalidOperationException) { rejectedDuplicatePosition = true; }
                Must(rejectedForeignKey && rejectedDuplicatePosition,
                    "native SQLite did not reject FK/or duplicate track position");
                Must(writer.QuickCheck() == "ok", "native fixture quick_check failed");
            }

            string before = Hash(path);
            var snapshot = new CatalogNavigationSnapshot();
            using (var catalog = CatalogService.OpenForTesting(path))
            {
                Must(catalog.QuickCheck() == "ok" &&
                    catalog.ForeignKeyViolationCount() == 0 &&
                    catalog.SchemaVersion == 4, "v4 integrity or FK failed");
                var counts = catalog.GetCounts();
                Must(counts.Releases == 3 && counts.Discs == 3 && counts.Tracks == 5,
                    "original CatalogService counts changed");
                catalog.EnableQueryOnlyForExperiment();
                Must(catalog.VerifyWriteIsRejectedForExperiment(),
                    "PRAGMA query_only failed to reject write");

                var releases = catalog.GetReleases();
                Must(releases.Count == 3 &&
                    releases.Select(r => r.Id).ToHashSet().SetEquals(new long[] {1,2,3}),
                    "GetReleases lost multi-release IDs");
                foreach (var r in releases)
                {
                    Must(catalog.GetRelease(r.Id)?.Album == r.Album,
                        "GetRelease disagrees with collection");
                    snapshot.Releases.Add(ReleaseRow(r));
                    var discs = catalog.GetDiscs(r.Id);
                    snapshot.Discs.Add(r.Id, new List<SyntheticTrackRow>());
                    foreach (var d in discs)
                    {
                        Must(d.ReleaseId == r.Id &&
                             catalog.GetDisc(d.Id)?.ReleaseId == r.Id,
                             "GetDiscs/GetDisc foreign release mismatch");
                        snapshot.Discs[r.Id].Add(DiscRow(r, d));
                        var tracks = catalog.GetTracks(d.Id);
                        Must(tracks.All(t => t.DiscId == d.Id) &&
                             tracks.Select(t => t.Id).Distinct().Count() == tracks.Count,
                             "GetTracks includes wrong disc or duplicate ID");
                        snapshot.Tracks.Add(d.Id, tracks.Select(TrackRow).ToList());
                        foreach (var track in tracks)
                            Must(catalog.GetTrack(track.Id)?.Title == track.Title,
                                "GetTrack differs from GetTracks");
                    }
                }
                Must(snapshot.ForDiscs(1).Count == 2 &&
                     snapshot.ForDiscs(2).Count == 1 &&
                     snapshot.ForDiscs(3).Count == 0 &&
                     snapshot.ForTracks(11).Count == 2 &&
                     snapshot.ForTracks(12).Count == 2 &&
                     snapshot.ForTracks(21).Count == 1, "hierarchical counts mismatch");
                Must(snapshot.ForDiscs(-1).Count == 0 &&
                     snapshot.ForTracks(-1).Count == 0 &&
                     catalog.GetReleases().Count == 3 &&
                     catalog.GetRelease(-1) == null &&
                     catalog.GetDisc(-1) == null &&
                     catalog.GetTrack(-1) == null, "missing ID accepted or stale navigation");
                Must(catalog.FindByToc(" 150 12000 24000 ").Count == 1 &&
                     catalog.FindByToc("absent TOC").Count == 0,
                     "real TOC lookup mismatch");
                var documents = catalog.GetSearchDocuments();
                Must(documents.Count == 6, "left-joined search documents lost empty release");
                var trackHit = CatalogService.SearchDocuments(documents, "Encore");
                Must(trackHit.ReleaseIds.SetEquals(new long[]{1}) &&
                     trackHit.DiscIds.SetEquals(new long[]{12}) &&
                     trackHit.TrackIds.SetEquals(new long[]{121}),
                     "SearchDocuments did not isolate a track and its parents");
                var emptyHit = CatalogService.SearchDocuments(documents, "Empty Album");
                Must(emptyHit.ReleaseIds.SetEquals(new long[]{3}) &&
                     emptyHit.DiscIds.Count == 0 && emptyHit.TrackIds.Count == 0,
                     "SearchDocuments omitted or invented child of empty release");
                Must(catalog.FindReleaseIds("Encore").SetEquals(new long[]{1}) &&
                     catalog.FindDiscIds(1, "Encore").SetEquals(new long[]{12}) &&
                     catalog.FindTrackIds(12, "Encore").SetEquals(new long[]{121}) &&
                     catalog.FindTrackIds(11, "Encore").Count == 0,
                     "original search-scoped SQL ignored hierarchy");
                Must(CatalogService.SearchDocuments(documents, "nonexistent-token").TrackIds.Count == 0,
                     "nonexistent query returned tracks");
                Must(snapshot.ForDiscs(1)[0].Identity == "disc:11" &&
                     snapshot.ForTracks(12)[0].Identity == "track:121",
                     "stable native entity identity mismatch");
            }
            Must(before == Hash(path), "query-only navigation unexpectedly modified fixture bytes");
            return snapshot;
        }
        finally
        {
            // Delete only the newly generated GUID-named disposable temp folder.
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }
}
