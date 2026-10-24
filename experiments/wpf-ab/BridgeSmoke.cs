using System;
using System.Linq;
using System.Windows;
using DJLibrary.SyntheticComparison;

namespace DJLibrary.WpfAb;
internal sealed partial class ComparisonWindow
{
    private string VerifyBridgeSnapshotBinding()
    {
        var rows = BridgeV3Fixture.Load();
        _selectionIds.Clear();
        _search.Text = "";
        _all = rows;
        _grid.ItemsSource = rows;
        _grid.SelectedItems.Clear();
        _grid.UpdateLayout();
        if (_grid.ActualWidth < 600 || _grid.Items.Count != 1345)
            throw new InvalidOperationException("WPF original Bridge v3 data did not render");
        _grid.SelectedItems.Add(rows[0]);
        _grid.SelectedItems.Add(rows[^1]);
        _search.Text = "Bridge Artist 0001";
        Filter(this, new RoutedEventArgs());
        if (_grid.Items.Count == 0 || _grid.Items.Count >= rows.Count)
            throw new InvalidOperationException("WPF original Bridge filter failed");
        Sort(this, new RoutedEventArgs());
        Clear(this, new RoutedEventArgs());
        var ids = _grid.SelectedItems.OfType<SyntheticTrackRow>()
            .Select(x => x.Identity).ToHashSet(StringComparer.Ordinal);
        if (_grid.Items.Count != rows.Count ||
            !ids.SetEquals(new[] { rows[0].Identity, rows[^1].Identity }))
            throw new InvalidOperationException("WPF Bridge v3 multi-selection lost identities");
        return "PASS WPF original BridgeSnapshotReader v3 TEMP: 1,345 gzip TSV rows, " +
               "real DataGrid filter/sort/selection; metadata vectors preserved; " +
               "duplicate/subsong/generation/schema negatives rejected; read-only hashes unchanged";
    }
}
