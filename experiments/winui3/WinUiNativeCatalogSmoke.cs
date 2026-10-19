using System;
using System.Linq;
using System.Threading.Tasks;
using DJLibrary.SyntheticComparison;
using Microsoft.UI.Xaml;

namespace DJLibrary.WinUiSpike;

public sealed partial class MainWindow
{
    // Loads real PRODUCTION CatalogNativeTrack model objects generated only in
    // memory, then binds their read-only view representation to actual TableView.
    private async Task<string> VerifyNativeCatalogBindingAsync()
    {
        var rows = NativeTrackProjection.ValidateReadOnlyContract(1_345);
        _selectionIds.Clear();
        _all = rows;
        SearchBox.Text = "";
        TracksGrid.ItemsSource = rows;
        TracksGrid.SelectedItems.Clear();
        await AwaitActualFrameAsync();
        await YieldToUiAsync();

        if (TracksGrid.ActualWidth < 600 || TracksGrid.Items.Count != rows.Count)
            throw new InvalidOperationException("Native DJ Library row projection not rendered in WinUI TableView");

        TracksGrid.SelectedItems.Add(rows[0]);
        TracksGrid.SelectedItems.Add(rows[^1]);
        SearchBox.Text = "Native Artist 001";
        Filter_Click(this, new RoutedEventArgs());
        Sort_Click(this, new RoutedEventArgs());
        Clear_Click(this, new RoutedEventArgs());
        await AwaitActualFrameAsync();

        var selected = TracksGrid.SelectedItems.OfType<SyntheticTrackRow>()
            .Select(r => r.Identity).ToHashSet(StringComparer.Ordinal);
        if (TracksGrid.Items.Count != rows.Count ||
            !selected.SetEquals(new[] { rows[0].Identity, rows[^1].Identity }))
            throw new InvalidOperationException(
                "WinUI native CatalogNativeTrack binding lost stable IDs through UI filter/sort/selection");

        return "PASS WinUI native DJ Library CatalogNativeTrack production model: " +
            $"{rows.Count} synthetic native records bound to real TableView, " +
            "display/filter/sort and identity-based multi-selection retained; " +
            "no catalog, bridge, SQLite, audio or user files touched";
    }
}
