using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DJLibrary
{
    // Human-readable, vendor-neutral interchange format. This is intentionally
    // NOT a full-fidelity SQLite backup: undo history and opaque legacy JSON
    // stay in the user's private SQLite catalog.
    public sealed class CatalogInterchangeDocument
    {
        public string Format { get; set; }
        public int Version { get; set; }
        public List<CatalogInterchangeRelease> Releases { get; set; }
    }

    public sealed class CatalogInterchangeRelease
    {
        public string AlbumArtist { get; set; }
        public string Album { get; set; }
        public string ReleaseDate { get; set; }
        public string Genre { get; set; }
        public string Label { get; set; }
        public string Catalog { get; set; }
        public string Country { get; set; }
        public int TotalDiscs { get; set; }
        public List<CatalogInterchangeDisc> Discs { get; set; }
    }

    public sealed class CatalogInterchangeDisc
    {
        public int DiscNumber { get; set; }
        public string Medium { get; set; }
        public string Toc { get; set; }
        public bool TocComplete { get; set; }
        public bool CdTextPresent { get; set; }
        public string CdTextStatus { get; set; }
        public double DurationSeconds { get; set; }
        public List<CatalogInterchangeTrack> Tracks { get; set; }
    }

    public sealed class CatalogInterchangeTrack
    {
        public int Position { get; set; }
        public string Artist { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public string ReleaseDate { get; set; }
        public string Genre { get; set; }
        public string LegacyGenre { get; set; }
        public double Bpm { get; set; }
        public double DurationSeconds { get; set; }
        public string LegacyArtistRaw { get; set; }
    }

    public sealed partial class CatalogService
    {
        private const string InterchangeFormatName = "dj-library-interchange";
        private const int InterchangeVersion = 1;
        private const int MaxInterchangeBytes = 128 * 1024 * 1024;

        private static JavaScriptSerializer NewInterchangeSerializer()
        {
            return new JavaScriptSerializer {
                MaxJsonLength = MaxInterchangeBytes,
                RecursionLimit = 100
            };
        }

        public void ExportJsonInterchange(string destination)
        {
            if (String.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("JSON-Exportziel fehlt.");
            string full = Path.GetFullPath(destination);
            if (String.Equals(full, Path.GetFullPath(_path), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Der aktive SQLite-Katalog darf nicht überschrieben werden.");

            CatalogInterchangeDocument document = new CatalogInterchangeDocument {
                Format = InterchangeFormatName,
                Version = InterchangeVersion,
                Releases = new List<CatalogInterchangeRelease>()
            };
            foreach (CatalogRelease r in GetReleases())
            {
                CatalogInterchangeRelease release = new CatalogInterchangeRelease {
                    AlbumArtist = r.AlbumArtist, Album = r.Album,
                    ReleaseDate = r.ReleaseDate, Genre = r.Genre,
                    Label = r.Label, Catalog = r.Catalog, Country = r.Country,
                    TotalDiscs = r.TotalDiscs,
                    Discs = new List<CatalogInterchangeDisc>()
                };
                foreach (CatalogDisc d in GetDiscs(r.Id))
                {
                    CatalogInterchangeDisc disc = new CatalogInterchangeDisc {
                        DiscNumber = d.DiscNumber, Medium = d.Medium,
                        Toc = d.Toc, TocComplete = d.TocComplete,
                        CdTextPresent = d.CdTextPresent, CdTextStatus = d.CdTextStatus,
                        DurationSeconds = d.DurationSeconds,
                        Tracks = new List<CatalogInterchangeTrack>()
                    };
                    foreach (CatalogTrack t in GetTracks(d.Id))
                    {
                        disc.Tracks.Add(new CatalogInterchangeTrack {
                            Position = t.Position, Artist = t.Artist, Title = t.Title,
                            Version = t.Version, ReleaseDate = t.ReleaseDate,
                            Genre = t.Genre, LegacyGenre = t.LegacyGenre,
                            Bpm = t.Bpm, DurationSeconds = t.DurationSeconds,
                            LegacyArtistRaw = t.LegacyArtistRaw
                        });
                    }
                    release.Discs.Add(disc);
                }
                document.Releases.Add(release);
            }

            string json = NewInterchangeSerializer().Serialize(document);
            string temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(full)) File.Replace(temp, full, null, true);
                else File.Move(temp, full);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public void ImportJsonInterchange(string source)
        {
            if (String.IsNullOrWhiteSpace(source) || !File.Exists(source))
                throw new FileNotFoundException("JSON-Importdatei fehlt.", source);
            FileInfo info = new FileInfo(source);
            if (info.Length > MaxInterchangeBytes)
                throw new InvalidDataException("JSON-Katalog überschreitet die Größenbegrenzung.");

            CatalogInterchangeDocument document =
                NewInterchangeSerializer().Deserialize<CatalogInterchangeDocument>(
                    File.ReadAllText(source, Encoding.UTF8));
            ValidateInterchange(document);

            // All adaptation is done in a disposable database. Only Restore()
            // can swap the live catalog, after its independent SQLite preflight.
            string dir = Path.Combine(Path.GetTempPath(),
                "DJLibrary-json-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string staged = Path.Combine(dir, "adapted.sqlite");
            try
            {
                using (CatalogService target = CreateEmptyForTesting(staged))
                {
                    target._db.Transaction(delegate
                    {
                        foreach (CatalogInterchangeRelease r in document.Releases)
                        {
                            target._db.Execute(
                                "INSERT INTO release(album_artist,album,release_date,genre,label,catalog,country,total_discs) VALUES(?,?,?,?,?,?,?,?)",
                                Nz(r.AlbumArtist), Nz(r.Album), Nz(r.ReleaseDate),
                                Nz(r.Genre), Nz(r.Label), Nz(r.Catalog),
                                Nz(r.Country), r.TotalDiscs);
                            long releaseId = target._db.LastInsertRowId;
                            foreach (CatalogInterchangeDisc d in r.Discs)
                            {
                                target._db.Execute(
                                    "INSERT INTO disc(release_id,disc_number,medium,toc,toc_complete,cd_text_present,cd_text_status,duration_seconds,legacy_json) VALUES(?,?,?,?,?,?,?,?,?)",
                                    releaseId, d.DiscNumber, Nz(d.Medium), Nz(d.Toc),
                                    d.TocComplete, d.CdTextPresent,
                                    String.IsNullOrWhiteSpace(d.CdTextStatus) ? "manual_unknown" : d.CdTextStatus,
                                    d.DurationSeconds, "{}");
                                long discId = target._db.LastInsertRowId;
                                foreach (CatalogInterchangeTrack t in d.Tracks)
                                {
                                    target._db.Execute(
                                        "INSERT INTO track(disc_id,position,artist,title,version,release_date,genre,legacy_genre,bpm,duration_seconds,legacy_artist_raw,legacy_json) VALUES(?,?,?,?,?,?,?,?,?,?,?,?)",
                                        discId, t.Position, Nz(t.Artist), Nz(t.Title),
                                        Nz(t.Version), Nz(t.ReleaseDate), Nz(t.Genre),
                                        Nz(t.LegacyGenre), t.Bpm, t.DurationSeconds,
                                        Nz(t.LegacyArtistRaw), "{}");
                                }
                            }
                        }
                    });
                    target.ValidateAndMigrate();
                    if (!String.Equals(target.QuickCheck(), "ok", StringComparison.OrdinalIgnoreCase) ||
                        target.ForeignKeyViolationCount() != 0)
                        throw new InvalidDataException("Angepasster JSON-Katalog ist ungültig.");
                }
                Restore(staged);
            }
            finally
            {
                DeleteSidecars(staged);
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        private static void ValidateInterchange(CatalogInterchangeDocument document)
        {
            if (document == null || document.Format != InterchangeFormatName ||
                document.Version != InterchangeVersion || document.Releases == null)
                throw new InvalidDataException("Unbekanntes DJ Library JSON-Austauschformat oder Schema.");

            if (document.Releases.Count > 100000)
                throw new InvalidDataException("Zu viele Releases im Austauschformat.");
            long discs = 0, tracks = 0;
            foreach (CatalogInterchangeRelease r in document.Releases)
            {
                if (r == null || r.TotalDiscs < 1 || r.Discs == null)
                    throw new InvalidDataException("Ungültige Release-Daten.");
                discs += r.Discs.Count;
                if (discs > 200000)
                    throw new InvalidDataException("Zu viele CDs im Austauschformat.");
                foreach (CatalogInterchangeDisc d in r.Discs)
                {
                    if (d == null || d.DiscNumber < 1 || d.Tracks == null ||
                        Double.IsNaN(d.DurationSeconds) || Double.IsInfinity(d.DurationSeconds) ||
                        d.DurationSeconds < 0)
                        throw new InvalidDataException("Ungültige CD-Daten.");
                    tracks += d.Tracks.Count;
                    if (tracks > 1000000)
                        throw new InvalidDataException("Zu viele Tracks im Austauschformat.");
                    HashSet<int> positions = new HashSet<int>();
                    foreach (CatalogInterchangeTrack t in d.Tracks)
                    {
                        if (t == null || t.Position < 1 || !positions.Add(t.Position) ||
                            Double.IsNaN(t.Bpm) || Double.IsInfinity(t.Bpm) || t.Bpm < 0 ||
                            Double.IsNaN(t.DurationSeconds) || Double.IsInfinity(t.DurationSeconds) ||
                            t.DurationSeconds < 0)
                            throw new InvalidDataException("Ungültige oder doppelte Track-Position im JSON-Katalog.");
                    }
                }
            }
        }
    }
}
