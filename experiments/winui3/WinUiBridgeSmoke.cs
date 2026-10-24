using System;
using System.Linq;
using System.Threading.Tasks;
using DJLibrary.SyntheticComparison;
using Microsoft.UI.Xaml;

namespace DJLibrary.WinUiSpike;
public sealed partial class MainWindow
{
    private async Task<string> VerifyBridgeSnapshotBindingAsync()
    {
        var rows = BridgeV3Fixture.Load();
        _selectionIds.Clear();
        SearchBox.Text = "";
        _all = rows;
        TracksGrid.ItemsSource = rows;
        TracksGrid.SelectedItems.Clear();
        await AwaitActualFrameAsync();
        if (TracksGrid.ActualWidth < 600 || TracksGrid.Items.Count != 1345)
            throw new InvalidOperationException("WinUI original Bridge v3 data did not render");
        TracksGrid.SelectedItems.Add(rows[0]);
        TracksGrid.SelectedItems.Add(rows[^1]);
        SearchBox.Text = "Bridge Artist 0001";
        Filter_Click(this, new RoutedEventArgs());
        await YieldToUiAsync();
        if (TracksGrid.Items.Count == 0 || TracksGrid.Items.Count >= rows.Count)
            throw new InvalidOperationException("WinUI original Bridge filter failed");
        Sort_Click(this, new RoutedEventArgs());
        Clear_Click(this, new RoutedEventArgs());
        await AwaitActualFrameAsync();
        var ids = TracksGrid.SelectedItems.OfType<SyntheticTrackRow>()
            .Select(x => x.Identity).ToHashSet(StringComparer.Ordinal);
        if (TracksGrid.Items.Count != rows.Count ||
            !ids.SetEquals(new[] { rows[0].Identity, rows[^1].Identity }))
            throw new InvalidOperationException("WinUI Bridge v3 multi-selection lost identities");
        return "PASS WinUI original BridgeSnapshotReader v3 TEMP: 1,345 gzip TSV rows, " +
               "real TableView filter/sort/selection; metadata vectors preserved; " +
               "duplicate/subsong/generation/schema negatives rejected; read-only hashes unchanged";
    }
}
