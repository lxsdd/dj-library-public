using System;
using System.Collections.Generic;
using System.Linq;

namespace DJLibrary.SyntheticComparison;

// Identity-based selection snapshot: filtering/sorting must not turn row indexes into identities.
public static class SyntheticSelection
{
    public static HashSet<string> Capture(IEnumerable<SyntheticTrackRow> selected) =>
        new HashSet<string>(selected.Select(x => x.Identity), StringComparer.Ordinal);

    public static List<SyntheticTrackRow> Restore(
        IEnumerable<SyntheticTrackRow> visibleRows, IReadOnlySet<string> selectedIds) =>
        visibleRows.Where(x => selectedIds.Contains(x.Identity)).ToList();
}
