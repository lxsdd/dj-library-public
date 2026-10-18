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
        return "PASS WinUI columns: in-memory JSON, programmatic reorder, pixel/Star/Auto widths, " +
            "visibility and exact restore to original state";
    }
}
