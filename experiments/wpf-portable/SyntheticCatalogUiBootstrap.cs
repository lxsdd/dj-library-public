using System;
using System.IO;

namespace DJLibrary
{
    // Disposable hosted-Windows UI fixture ONLY, never the installed app.
    internal static class SyntheticCatalogUiBootstrap
    {
        public static int Main(string[] args)
        {
            try
            {
                if (!String.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                        "true", StringComparison.OrdinalIgnoreCase) ||
                    args.Length != 1)
                    throw new InvalidOperationException("GitHub disposable CI only");
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string expected = Path.GetFullPath(Path.Combine(appData, "DJ Library", "catalog-v0.4.sqlite"));
                string supplied = Path.GetFullPath(args[0]);
                if (!String.Equals(expected, supplied, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(expected))
                    throw new InvalidOperationException("Refuse non-isolated SQLite fixture path");

                using (CatalogService catalog = CatalogService.OpenForTesting(expected))
                {
                    CatalogCounts before = catalog.GetCounts();
                    if (before.Releases != 0 || before.Discs != 0 || before.Tracks != 0)
                        throw new InvalidOperationException("Refuse nonempty catalog; only synthetic new profile allowed");
                    long id = catalog.CreateRelease(new CatalogRelease
                    {
                        AlbumArtist = "CI Synthetic Artist",
                        Album = "CI Synthetic Album",
                        Genre = "CI Test",
                        TotalDiscs = 2
                    });
                    long disc1 = catalog.AddDisc(new CatalogDisc
                    {
                        ReleaseId = id, DiscNumber = 1, Medium = "CD",
                        Toc = "150 360148", TocComplete = true
                    });
                    long disc2 = catalog.AddDisc(new CatalogDisc
                    {
                        ReleaseId = id, DiscNumber = 2, Medium = "CD",
                        Toc = "150 360149", TocComplete = true
                    });
                    catalog.AddTrack(new CatalogTrack
                    {
                        DiscId = disc1, Position = 1, Artist = "CI Synthetic Artist",
                        Title = "Photon Fixture Only", Bpm = 128.5
                    });
                    catalog.AddTrack(new CatalogTrack
                    {
                        DiscId = disc2, Position = 1, Artist = "CI Synthetic Artist",
                        Title = "Nebula Fixture Only", Bpm = 133.0
                    });
                    CatalogCounts after = catalog.GetCounts();
                    if (after.Releases != 1 || after.Discs != 2 || after.Tracks != 2 ||
                        catalog.ForeignKeyViolationCount() != 0)
                        throw new InvalidOperationException("Disposable fixture count or FK failure");
                    Console.WriteLine("PASS GUI synthetic fixture created: 1 release, 2 discs, 2 tracks in isolated runner profile");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL UI fixture: " + ex);
                return 1;
            }
        }
    }
}
