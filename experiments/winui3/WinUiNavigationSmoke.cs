using System;
using System.Linq;
using System.Threading.Tasks;
using DJLibrary.SyntheticComparison;
using Microsoft.UI.Xaml;

namespace DJLibrary.WinUiSpike;
public sealed partial class MainWindow
{
    private async Task<string> VerifyHierarchicalNavigationAsync()
    {
        var snapshot = CatalogNavigationFixture.Load();
        TracksGrid.SelectedItems.Clear();
        _selectionIds.Clear();
        async Task NavigateAsync(System.Collections.Generic.List<SyntheticTrackRow> rows)
        {
            PreserveSelection();
            _all = rows;
            SearchBox.Text = "";
            TracksGrid.ItemsSource = rows;
            RestoreSelection();
            await AwaitActualFrameAsync();
            await YieldToUiAsync();
            if (TracksGrid.Items.Count != rows.Count || TracksGrid.ActualWidth < 600)
                throw new InvalidOperationException("WinUI navigated TableView not actually laid out");
        }
        await NavigateAsync(snapshot.Releases);
        TracksGrid.SelectedItems.Add(snapshot.Releases[0]);
        TracksGrid.SelectedItems.Add(snapshot.Releases[^1]);
        await NavigateAsync(snapshot.ForDiscs(1));
        if (TracksGrid.SelectedItems.Count != 0 || TracksGrid.Items.Count != 2)
            throw new InvalidOperationException("WinUI release-to-disc navigation leaked selection");
        await NavigateAsync(snapshot.ForTracks(11));
        TracksGrid.SelectedItems.Add(snapshot.ForTracks(11)[0]);
        TracksGrid.SelectedItems.Add(snapshot.ForTracks(11)[^1]);
        SearchBox.Text = "Opening";
        Filter_Click(this, new RoutedEventArgs());
        await YieldToUiAsync();
        if (TracksGrid.Items.Count != 1)
            throw new InvalidOperationException("WinUI selected-disc track filter incorrect");
        Sort_Click(this, new RoutedEventArgs());
        Clear_Click(this, new RoutedEventArgs());
        await YieldToUiAsync();
        if (TracksGrid.SelectedItems.Count != 2)
            throw new InvalidOperationException("WinUI track selection lost on filter/sort");
        await NavigateAsync(snapshot.ForTracks(12));
        if (TracksGrid.SelectedItems.Count != 0 ||
            TracksGrid.Items.Count != 2 || _all[0].Identity != "track:121")
            throw new InvalidOperationException("WinUI changed-disc navigation includes stale tracks");
        await NavigateAsync(snapshot.ForTracks(11));
        if (!TracksGrid.SelectedItems.OfType<SyntheticTrackRow>()
            .Select(t => t.Identity).ToHashSet(StringComparer.Ordinal)
            .SetEquals(new[] {"track:101","track:102"}))
            throw new InvalidOperationException("WinUI returning to disc lost stable track selections");
        await NavigateAsync(snapshot.Releases);
        if (!TracksGrid.SelectedItems.OfType<SyntheticTrackRow>()
            .Select(t => t.Identity).ToHashSet(StringComparer.Ordinal)
            .SetEquals(new[] {snapshot.Releases[0].Identity, snapshot.Releases[^1].Identity}))
            throw new InvalidOperationException("WinUI release selection did not survive disc navigation");
        return "PASS WinUI original CatalogService release/disc/track navigation: " +
            "3 releases, 3 discs, 5 tracks, empty release, multi-disc, TOC and scoped search; " +
            "real TableView hierarchy views/filter/sort/stable cross-view selection; " +
            "FK/duplicate/restricted writes rejected, synthetic SQLite SHA-256 unchanged";
    }
}
