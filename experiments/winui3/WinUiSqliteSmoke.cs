using System;
using System.Linq;
using System.Threading.Tasks;
using DJLibrary.SyntheticComparison;
using Microsoft.UI.Xaml;

namespace DJLibrary.WinUiSpike;
public sealed partial class MainWindow
{
    private async Task<string> VerifyDisposableSqliteBindingAsync()
    {
        var rows = NativeSqliteFixture.LoadReadOnlySqlite(54_129);
        _selectionIds.Clear();
        _all = rows;
        SearchBox.Text = "";
        TracksGrid.ItemsSource = rows;
        TracksGrid.SelectedItems.Clear();
        await AwaitActualFrameAsync();
        await YieldToUiAsync();
        if (TracksGrid.Items.Count != rows.Count || TracksGrid.ActualWidth < 600)
            throw new InvalidOperationException("WinUI actual CatalogService SQLite query did not render");
        TracksGrid.SelectedItems.Add(rows[0]);
        TracksGrid.SelectedItems.Add(rows[^1]);
        SearchBox.Text = "Native Artist 001";
        Filter_Click(this, new RoutedEventArgs());
        Sort_Click(this, new RoutedEventArgs());
        Clear_Click(this, new RoutedEventArgs());
        await AwaitActualFrameAsync();
        var ids = TracksGrid.SelectedItems.OfType<SyntheticTrackRow>()
            .Select(r => r.Identity).ToHashSet(StringComparer.Ordinal);
        if (TracksGrid.Items.Count != rows.Count ||
            !ids.SetEquals(new[] { rows[0].Identity, rows[^1].Identity }))
            throw new InvalidOperationException(
                "WinUI SQLite-backed native row projection lost selected catalog identities");
        return "PASS WinUI read-only SQLite CatalogService.GetNativeTracks: " +
            "original native WinSqlite SQL, 54,129 temporary database tracks, " +
            "real TableView filter/sort and stable multi-selection; " +
            "PRAGMA query_only write rejection and unchanged fixture SHA-256";
    }
}
