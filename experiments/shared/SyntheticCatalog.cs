using System;
using System.Collections.Generic;
using System.Linq;

namespace DJLibrary.SyntheticComparison;

// Intentionally self-contained, deterministic and free of catalog/DB/filesystem access.
// The same source file is linked into both UI experiments and the contract executable.
public sealed class SyntheticTrackRow
{
    public string Identity { get; }
    public string Artist { get; }
    public string Title { get; }
    public string Genre { get; }
    public string Year { get; }
    public string Duration { get; }

    public SyntheticTrackRow(string identity, string artist, string title, string genre, string year, string duration)
    {
        Identity = identity;
        Artist = artist;
        Title = title;
        Genre = genre;
        Year = year;
        Duration = duration;
    }
}

public static class SyntheticCatalog
{
    public static readonly int[] SupportedSizes = { 1_345, 15_000, 54_129, 100_000 };

    public static List<SyntheticTrackRow> Generate(int count)
    {
        if (!SupportedSizes.Contains(count)) throw new ArgumentOutOfRangeException(nameof(count));
        var rows = new List<SyntheticTrackRow>(count);
        for (int n = 1; n <= count; n++)
        {
            rows.Add(new SyntheticTrackRow(
                "synthetic-" + n.ToString("D7"),
                "Artist " + (n % 1100).ToString("D4"),
                "Track " + n.ToString("D6"),
                (n % 4) switch { 0 => "House", 1 => "Trance", 2 => "Techno", _ => "Other" },
                (1990 + n % 37).ToString(),
                $"{3 + n % 7:00}:{n % 60:00}"));
        }
        return rows;
    }

    public static List<SyntheticTrackRow> Filter(IEnumerable<SyntheticTrackRow> rows, string? query)
    {
        string term = (query ?? string.Empty).Trim();
        return term.Length == 0 ? rows.ToList() :
            rows.Where(x => x.Artist.Contains(term, StringComparison.OrdinalIgnoreCase)
                         || x.Title.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static List<SyntheticTrackRow> SortByTitle(IEnumerable<SyntheticTrackRow> rows) =>
        rows.OrderBy(x => x.Title, StringComparer.Ordinal)
            .ThenBy(x => x.Identity, StringComparer.Ordinal).ToList();

    // Stable cross-process identity fingerprint, not a cryptographic integrity digest.
    public static string Fingerprint(IEnumerable<SyntheticTrackRow> rows)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (var row in rows)
            {
                foreach (char c in row.Identity)
                {
                    hash ^= c;
                    hash *= 1099511628211UL;
                }
                hash ^= 0xff;
                hash *= 1099511628211UL;
            }
            return hash.ToString("X16");
        }
    }
}
