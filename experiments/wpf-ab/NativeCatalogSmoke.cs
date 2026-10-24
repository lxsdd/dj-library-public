using System;
using System.Linq;
using System.Windows;
using DJLibrary.SyntheticComparison;

namespace DJLibrary.WpfAb;

internal sealed partial class ComparisonWindow
{
    // Exercise the same original product model and shared projection in a
    // real WPF DataGrid window, independently from the WinUI presentation.
    internal string VerifyNativeCatalogBinding()
    {
        var rows = NativeTrackProjection.ValidateReadOnlyContract(1_345);
        _selectionIds.Clear();
        _all = rows;
        _search.Text = "";
        _grid.ItemsSource = rows;
        _grid.SelectedItems.Clear();
        _grid.UpdateLayout();

        if (_grid.ActualWidth < 600 || _grid.Items.Count != rows.Count)
            throw new InvalidOperationException("Native DJ Library row projection not rendered in WPF DataGrid");
        _grid.SelectedItems.Add(rows[0]);
        _grid.SelectedItems.Add(rows[^1]);
        _search.Text = "Native Artist 001";
        Filter(this, new RoutedEventArgs());
        Sort(this, new RoutedEventArgs());
        Clear(this, new RoutedEventArgs());
        _grid.UpdateLayout();

        var selected = _grid.SelectedItems.OfType<SyntheticTrackRow>()
            .Select(r => r.Identity).ToHashSet(StringComparer.Ordinal);
        if (_grid.Items.Count != rows.Count ||
            !selected.SetEquals(new[] { rows[0].Identity, rows[^1].Identity }))
            throw new InvalidOperationException(
                "WPF native CatalogNativeTrack binding lost stable IDs through UI filter/sort/selection");

        return "PASS WPF native DJ Library CatalogNativeTrack production model: " +
            $"{rows.Count} synthetic native records bound to real DataGrid, " +
            "display/filter/sort and identity-based multi-selection retained; " +
            "no catalog, bridge, SQLite, audio or user files touched";
    }
}
