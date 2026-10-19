using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DJLibrary;

namespace DJLibrary.SyntheticComparison;

// Read-only adapter over the ACTUAL production CatalogNativeTrack contract.
// Does not open the catalog, bridge, disk, network or any user files.
public static class NativeTrackProjection
{
    public static List<SyntheticTrackRow> Project(IEnumerable<CatalogNativeTrack> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var rows = new List<SyntheticTrackRow>();
        var seen = new HashSet<long>();
        foreach (var track in source)
        {
            if (track == null || track.Id <= 0 || track.DiscId <= 0 ||
                track.Position <= 0 || !seen.Add(track.Id) ||
                double.IsNaN(track.DurationSeconds) ||
                double.IsInfinity(track.DurationSeconds) ||
                track.DurationSeconds < 0)
                throw new InvalidOperationException("Unsafe or duplicate native track identity/metadata");
            string year = track.ReleaseDate?.Length >= 4
                ? track.ReleaseDate.Substring(0, 4) : "";
            string duration = TimeSpan.FromSeconds(track.DurationSeconds)
                .ToString(@"mm\:ss", CultureInfo.InvariantCulture);
            rows.Add(new SyntheticTrackRow(
                "native-track-" + track.Id.ToString("D12", CultureInfo.InvariantCulture),
                track.Artist ?? "", track.Title ?? "", track.Genre ?? "", year, duration));
        }
        return rows;
    }

    public static List<CatalogNativeTrack> CreateSyntheticNativeFixture(int count)
    {
        if (count < 1 || count > 100000)
            throw new ArgumentOutOfRangeException(nameof(count));
        var result = new List<CatalogNativeTrack>(count);
        for (int i = 1; i <= count; i++)
            result.Add(new CatalogNativeTrack
            {
                Id = i,
                DiscId = 100 + ((i - 1) / 12),
                Position = (i - 1) % 12 + 1,
                Artist = "Native Artist " + (i % 131).ToString("D3", CultureInfo.InvariantCulture),
                Title = "Native Title " + i.ToString("D6", CultureInfo.InvariantCulture),
                Genre = i % 2 == 0 ? "Trance" : "House",
                ReleaseDate = "2026-10-19",
                DurationSeconds = 180 + i % 70,
                Bpm = 120 + i % 24,
                LegacyJson = "{}"
            });
        return result;
    }

    public static List<SyntheticTrackRow> ValidateReadOnlyContract(int count)
    {
        var source = CreateSyntheticNativeFixture(count);
        long beforeFirstId = source[0].Id;
        double beforeDuration = source[0].DurationSeconds;
        var rows = Project(source);
        if (rows.Count != count ||
            rows[0].Identity != "native-track-000000000001" ||
            rows[^1].Identity != "native-track-" + count.ToString("D12", CultureInfo.InvariantCulture) ||
            rows[0].Artist != source[0].Artist ||
            rows[0].Title != source[0].Title ||
            rows[0].Genre != source[0].Genre ||
            rows[0].Year != "2026" ||
            rows[0].Duration != "03:01" ||
            source[0].Id != beforeFirstId ||
            source[0].DurationSeconds != beforeDuration)
            throw new InvalidOperationException("Native catalog projection lost source identity, fields or immutability");

        var filtered = SyntheticCatalog.Filter(rows, "Native Artist 001");
        if (filtered.Count == 0 || filtered.Count >= count)
            throw new InvalidOperationException("Native projection filter invalid");
        var sorted = SyntheticCatalog.SortByTitle(rows);
        if (sorted.Count != count || sorted[0].Identity != rows[0].Identity)
            throw new InvalidOperationException("Native projection sort lost identities");
        var selection = SyntheticSelection.Capture(new[] { rows[0], rows[^1] });
        if (SyntheticSelection.Restore(sorted, selection).Count != 2)
            throw new InvalidOperationException("Native projection selection lost stable IDs");

        void ExpectInvalid(Action test)
        {
            try { test(); }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("Native projection accepted unsafe source fixture");
        }

        var duplicated = CreateSyntheticNativeFixture(2);
        duplicated[1].Id = duplicated[0].Id;
        ExpectInvalid(() => Project(duplicated));
        var invalid = CreateSyntheticNativeFixture(1);
        invalid[0].DurationSeconds = double.NaN;
        ExpectInvalid(() => Project(invalid));
        invalid[0].DurationSeconds = 90;
        invalid[0].Id = 0;
        ExpectInvalid(() => Project(invalid));
        invalid[0].Id = 1;
        invalid[0].DiscId = 0;
        ExpectInvalid(() => Project(invalid));
        return rows;
    }
}
