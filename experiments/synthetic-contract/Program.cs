using DJLibrary.SyntheticComparison;

foreach (int size in SyntheticCatalog.SupportedSizes)
{
    var rows = SyntheticCatalog.Generate(size);
    if (rows.Count != size) throw new Exception($"Bad row count for {size}");
    if (rows[0].Identity != "synthetic-0000001" || rows[^1].Identity != $"synthetic-{size:D7}")
        throw new Exception("Unstable synthetic identities");
    if (rows.Select(x => x.Identity).Distinct(StringComparer.Ordinal).Count() != size)
        throw new Exception("Duplicate synthetic identity");
    if (SyntheticCatalog.Fingerprint(rows) != SyntheticCatalog.Fingerprint(SyntheticCatalog.Generate(size)))
        throw new Exception("Non-deterministic identity fingerprint");
    var filter = SyntheticCatalog.Filter(rows, "  aRtIsT 0001  ");
    if (filter.Count == 0 || filter.Any(x => !x.Artist.Contains("Artist 0001", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("Filter contract failed");
    var sorted = SyntheticCatalog.SortByTitle(rows.AsEnumerable().Reverse());
    if (sorted.Count != size || sorted[0].Title != "Track 000001" || sorted[^1].Title != $"Track {size:D6}")
        throw new Exception("Sort contract failed");
    if (SyntheticCatalog.Filter(rows, "").Count != size) throw new Exception("Empty filter lost rows");
    var chosen = new[] { rows[0], rows[size / 2], rows[^1] };
    var selectedIds = SyntheticSelection.Capture(chosen);
    var reversed = SyntheticCatalog.SortByTitle(rows.AsEnumerable().Reverse());
    var restored = SyntheticSelection.Restore(reversed, selectedIds);
    if (restored.Count != 3 || restored.Select(x => x.Identity).ToHashSet().SetEquals(selectedIds) == false)
        throw new Exception("Selection lost identities after sorting");
    var hidden = SyntheticCatalog.Filter(rows, "Artist 0001");
    var visibleSelection = SyntheticSelection.Restore(hidden, selectedIds);
    if (visibleSelection.Any(x => !selectedIds.Contains(x.Identity)))
        throw new Exception("Selection restored unrelated row after filtering");
    if (SyntheticSelection.Restore(rows, new HashSet<string>(StringComparer.Ordinal)).Count != 0)
        throw new Exception("Empty selection unexpectedly restored rows");
    Console.WriteLine($"PASS {size:N0} rows; hash={SyntheticCatalog.Fingerprint(rows)}; filter={filter.Count:N0}");
}
Console.WriteLine("PASS: identical synthetic model/filter/sort contract for WPF and WinUI");
