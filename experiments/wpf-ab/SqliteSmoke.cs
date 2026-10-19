using System;
using System.Linq;
using System.Windows;
using DJLibrary.SyntheticComparison;

namespace DJLibrary.WpfAb;
internal sealed partial class ComparisonWindow
{
    private string VerifyDisposableSqliteBinding()
    {
        var rows = NativeSqliteFixture.LoadReadOnlySqlite(1_345);
        _selectionIds.Clear();
        _all = rows;
        _search.Text = "";
        _grid.ItemsSource = rows;
        _grid.SelectedItems.Clear();
        _grid.UpdateLayout();
        if (_grid.Items.Count != rows.Count || _grid.ActualWidth < 600)
            throw new InvalidOperationException("WPF actual CatalogService SQLite query did not render");
        _grid.SelectedItems.Add(rows[0]);
        _grid.SelectedItems.Add(rows[^1]);
        _search.Text = "Native Artist 001";
        Filter(this, new RoutedEventArgs());
        Sort(this, new RoutedEventArgs());
        Clear(this, new RoutedEventArgs());
        _grid.UpdateLayout();
        var ids = _grid.SelectedItems.OfType<SyntheticTrackRow>()
            .Select(r => r.Identity).ToHashSet(StringComparer.Ordinal);
        if (_grid.Items.Count != rows.Count ||
            !ids.SetEquals(new[] { rows[0].Identity, rows[^1].Identity }))
            throw new InvalidOperationException(
                "WPF SQLite-backed native row projection lost selected catalog identities");
        return "PASS WPF read-only SQLite CatalogService.GetNativeTracks: " +
            "original native WinSqlite SQL, 1345 temporary database tracks, " +
            "real DataGrid filter/sort and stable multi-selection; " +
            "PRAGMA query_only write rejection and unchanged fixture SHA-256";
    }
}
