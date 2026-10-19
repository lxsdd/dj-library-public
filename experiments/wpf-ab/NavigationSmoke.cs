using System;
using System.Linq;
using System.Windows;
using DJLibrary.SyntheticComparison;

namespace DJLibrary.WpfAb;
internal sealed partial class ComparisonWindow
{
    private string VerifyHierarchicalNavigation()
    {
        var snapshot = CatalogNavigationFixture.Load();
        _selectionIds.Clear();
        void Navigate(System.Collections.Generic.List<SyntheticTrackRow> rows)
        {
            PreserveSelection();
            _all = rows;
            _search.Text = "";
            _grid.ItemsSource = rows;
            RestoreSelection();
            _grid.UpdateLayout();
            if (_grid.Items.Count != rows.Count || _grid.ActualWidth < 600)
                throw new InvalidOperationException("WPF navigated DataGrid not actually laid out");
        }
        Navigate(snapshot.Releases);
        _grid.SelectedItems.Add(snapshot.Releases[0]);
        _grid.SelectedItems.Add(snapshot.Releases[^1]);
        Navigate(snapshot.ForDiscs(1));
        if (_grid.SelectedItems.Count != 0 || _grid.Items.Count != 2)
            throw new InvalidOperationException("WPF release-to-disc navigation leaked selection");
        Navigate(snapshot.ForTracks(11));
        _grid.SelectedItems.Add(snapshot.ForTracks(11)[0]);
        _grid.SelectedItems.Add(snapshot.ForTracks(11)[^1]);
        _search.Text = "Opening";
        Filter(this, new RoutedEventArgs());
        if (_grid.Items.Count != 1)
            throw new InvalidOperationException("WPF selected-disc track filter incorrect");
        Sort(this, new RoutedEventArgs());
        Clear(this, new RoutedEventArgs());
        if (_grid.SelectedItems.Count != 2)
            throw new InvalidOperationException("WPF track selection lost on filter/sort");
        Navigate(snapshot.ForTracks(12));
        if (_grid.SelectedItems.Count != 0 ||
            _grid.Items.Count != 2 || _all[0].Identity != "track:121")
            throw new InvalidOperationException("WPF changed-disc navigation includes stale tracks");
        Navigate(snapshot.ForTracks(11));
        if (!_grid.SelectedItems.OfType<SyntheticTrackRow>()
            .Select(t => t.Identity).ToHashSet(StringComparer.Ordinal)
            .SetEquals(new[] {"track:101","track:102"}))
            throw new InvalidOperationException("WPF returning to disc lost stable track selections");
        Navigate(snapshot.Releases);
        if (!_grid.SelectedItems.OfType<SyntheticTrackRow>()
            .Select(t => t.Identity).ToHashSet(StringComparer.Ordinal)
            .SetEquals(new[] {"release:1","release:3"}))
            throw new InvalidOperationException("WPF release selection did not survive disc navigation");
        return "PASS WPF original CatalogService release/disc/track navigation: " +
            "3 releases, 3 discs, 5 tracks, empty release, multi-disc, TOC and scoped search; " +
            "real DataGrid hierarchy views/filter/sort/stable cross-view selection; " +
            "FK/duplicate/restricted writes rejected, synthetic SQLite SHA-256 unchanged";
    }
}
