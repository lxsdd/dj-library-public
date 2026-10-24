using System;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace DJLibrary.WinUiSpike;

// Deliberately not a production settings serializer. Uses only a JSON memory
// round trip; it probes whether the 1.5.0 TableView public column surface
// provides the semantics necessary to design one safely later.
public sealed partial class MainWindow
{
    private sealed record ColumnState(
        string Key, int DisplayIndex, double Width, string WidthUnit, bool Visible);

    private ColumnState[] CaptureColumns()
    {
        return TracksGrid.Columns.Select((column, i) =>
            new ColumnState(
                column.Header?.ToString() ?? string.Empty, i,
                column.Width.Value, column.Width.GridUnitType.ToString(),
                column.Visibility == Visibility.Visible)).ToArray();
    }

    private void ApplyColumns(ColumnState[] snapshot)
    {
        var current = TracksGrid.Columns;
        if (snapshot.Length != current.Count ||
            snapshot.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new InvalidOperationException("WinUI named column state is incomplete or duplicated");

        // Validate ALL incoming settings before moving/resizing any visible
        // column. A partially corrupt user-profile layout is not allowed to
        // leave the current table in a half-applied state.
        var existingKeys = current.Select(c => c.Header?.ToString() ?? string.Empty)
            .OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var incomingKeys = snapshot.Select(c => c.Key)
            .OrderBy(k => k, StringComparer.Ordinal).ToArray();
        if (!existingKeys.SequenceEqual(incomingKeys, StringComparer.Ordinal))
            throw new InvalidOperationException("WinUI saved column keys do not match the current schema");
        if (!snapshot.Select(c => c.DisplayIndex).OrderBy(i => i)
            .SequenceEqual(Enumerable.Range(0, current.Count)))
            throw new InvalidOperationException("WinUI saved column indexes are not a valid permutation");
        foreach (var state in snapshot)
        {
            if (!Enum.TryParse<GridUnitType>(state.WidthUnit, out var unit) ||
                !Enum.IsDefined(unit) ||
                double.IsNaN(state.Width) || double.IsInfinity(state.Width) ||
                (unit == GridUnitType.Pixel && state.Width <= 0) ||
                (unit == GridUnitType.Star && state.Width <= 0))
                throw new InvalidOperationException(
                    "WinUI saved column has an invalid width: " + state.Key);
        }

        foreach (var state in snapshot.OrderBy(x => x.DisplayIndex))
        {
            int index = -1;
            for (int i = 0; i < current.Count; i++)
            {
                if (string.Equals(current[i].Header?.ToString(), state.Key, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0) throw new InvalidOperationException("Unknown WinUI column key " + state.Key);
            current.Move(index, state.DisplayIndex);
            var column = current[state.DisplayIndex];
            if (!Enum.TryParse<GridUnitType>(state.WidthUnit, out var unit))
                throw new InvalidOperationException("Unknown WinUI GridLength unit " + state.WidthUnit);
            column.Width = new GridLength(state.Width, unit);
            column.Visibility = state.Visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private string VerifySyntheticColumnStateRoundTrip()
    {
        var original = CaptureColumns();
        if (original.Length < 4) throw new InvalidOperationException("Expected six named TableView columns");
        var first = TracksGrid.Columns[0];
        TracksGrid.Columns.Move(0, 2);
        first.Width = new GridLength(2, GridUnitType.Star);
        first.Visibility = Visibility.Collapsed;
        TracksGrid.Columns[0].Width = new GridLength(230, GridUnitType.Pixel);
        TracksGrid.Columns[1].Width = GridLength.Auto;

        var expected = CaptureColumns();
        if (expected[2].Key != original[0].Key ||
            expected[2].Visible ||
            expected[2].WidthUnit != GridUnitType.Star.ToString())
            throw new InvalidOperationException("TableView did not honor reordered and hidden Star column");

        // Serialize in memory rather than touching any product/user profile.
        string serialized = JsonSerializer.Serialize(expected);
        var restored = JsonSerializer.Deserialize<ColumnState[]>(serialized) ??
            throw new InvalidOperationException("In-memory column JSON round-trip was null");
        ApplyColumns(original);
        ApplyColumns(restored);

        var observed = CaptureColumns();
        if (!JsonSerializer.Serialize(observed).Equals(serialized, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "WinUI column state did not restore order, width unit and visibility");

        ApplyColumns(original);

        // Invalid persisted layouts must be rejected atomically. Verify no
        // partial mutation of the already-rendered table on each failure.
        var stable = CaptureColumns();
        AssertRejectedLayout(stable.Select((x, i) =>
            i == 1 ? x with { Key = stable[0].Key } : x).ToArray(), "duplicate key");
        AssertRejectedLayout(stable.Select((x, i) =>
            i == 1 ? x with { Key = "UnknownDeletedColumn" } : x).ToArray(), "unknown column");
        AssertRejectedLayout(stable.Select((x, i) =>
            i == 1 ? x with { DisplayIndex = 0 } : x).ToArray(), "duplicate display index");
        AssertRejectedLayout(stable.Select((x, i) =>
            i == 1 ? x with { WidthUnit = "UnknownWidthUnit" } : x).ToArray(), "invalid width unit");

        return "PASS WinUI columns: in-memory JSON, programmatic reorder, pixel/Star/Auto widths, " +
            "visibility and exact restore to original state; corrupt snapshots rejected without mutation";
    }

    private void AssertRejectedLayout(ColumnState[] corrupt, string scenario)
    {
        var before = JsonSerializer.Serialize(CaptureColumns());
        bool rejected = false;
        try { ApplyColumns(corrupt); }
        catch (InvalidOperationException) { rejected = true; }
        if (!rejected)
            throw new InvalidOperationException(
                "WinUI corrupt column snapshot not rejected: " + scenario);
        if (!string.Equals(before, JsonSerializer.Serialize(CaptureColumns()), StringComparison.Ordinal))
            throw new InvalidOperationException(
                "WinUI corrupt column snapshot caused partial mutation: " + scenario);
    }
}
