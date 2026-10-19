using System;
using System.Linq;
using System.Threading.Tasks;
using DJLibrary.SyntheticComparison;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DJLibrary.WinUiSpike;

public sealed partial class MainWindow
{
    private CatalogNavigationSnapshot? _navigation;

    private void ResetNavigationForSyntheticLoad()
    {
        _navigation = null;
        NavigationReleaseSelector.ItemsSource = null;
        NavigationDiscSelector.ItemsSource = null;
    }

    private void ShowNavigationRows(System.Collections.Generic.List<SyntheticTrackRow> rows)
    {
        PreserveSelection();
        SearchBox.Text = "";
        _all = rows;
        TracksGrid.ItemsSource = rows;
        RestoreSelection();
        StatusText.Text = "Read-only TEMP CatalogService: " + rows.Count +
            " entries. No existing library, audio, settings or bridge accessed.";
    }

    private void NavigationLoad_Click(object sender, RoutedEventArgs e)
    {
        TracksGrid.SelectedItems.Clear();
        _selectionIds.Clear();
        _navigation = CatalogNavigationFixture.Load();
        NavigationDiscSelector.ItemsSource = null;
        NavigationReleaseSelector.ItemsSource = _navigation.Releases;
        NavigationReleaseSelector.SelectedIndex = -1;
        ShowNavigationRows(_navigation.Releases);
    }

    private void NavigationReleases_Click(object sender, RoutedEventArgs e)
    {
        if (_navigation != null) ShowNavigationRows(_navigation.Releases);
    }

    private void NavigationRelease_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_navigation == null ||
            NavigationReleaseSelector.SelectedItem is not SyntheticTrackRow row ||
            !row.Identity.StartsWith("release:", StringComparison.Ordinal) ||
            !long.TryParse(row.Identity.Substring("release:".Length), out long releaseId))
            return;
        var discs = _navigation.ForDiscs(releaseId);
        NavigationDiscSelector.ItemsSource = discs;
        NavigationDiscSelector.SelectedIndex = -1;
        ShowNavigationRows(discs);
    }

    private void NavigationDisc_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_navigation == null ||
            NavigationDiscSelector.SelectedItem is not SyntheticTrackRow row ||
            !row.Identity.StartsWith("disc:", StringComparison.Ordinal) ||
            !long.TryParse(row.Identity.Substring("disc:".Length), out long discId))
            return;
        ShowNavigationRows(_navigation.ForTracks(discId));
    }

    private async Task<string> VerifyNavigationSelectorsAsync()
    {
        NavigationLoad_Click(this, new RoutedEventArgs());
        await AwaitActualFrameAsync();
        if (_navigation == null || NavigationReleaseSelector.Items.Count != 3 ||
            TracksGrid.Items.Count != 3)
            throw new InvalidOperationException("WinUI release selector not bound");
        NavigationReleaseSelector.SelectedItem = _navigation.Releases.Single(r => r.Identity == "release:1");
        await YieldToUiAsync();
        if (NavigationDiscSelector.Items.Count != 2 ||
            TracksGrid.Items.Count != 2 || _all[0].Identity != "disc:11")
            throw new InvalidOperationException("WinUI release SelectionChanged not wired");
        NavigationDiscSelector.SelectedItem = _navigation.ForDiscs(1)[0];
        await YieldToUiAsync();
        if (TracksGrid.Items.Count != 2 || _all[0].Identity != "track:101")
            throw new InvalidOperationException("WinUI disc SelectionChanged not wired");
        SearchBox.Text = "Opening";
        Filter_Click(this, new RoutedEventArgs());
        await YieldToUiAsync();
        if (TracksGrid.Items.Count != 1)
            throw new InvalidOperationException("WinUI navigation filtering failed");
        Clear_Click(this, new RoutedEventArgs());
        NavigationReleases_Click(this, new RoutedEventArgs());
        await AwaitActualFrameAsync();
        if (TracksGrid.Items.Count != 3 || TracksGrid.ActualWidth < 600)
            throw new InvalidOperationException("WinUI release navigation restore failed");
        return "PASS WinUI interactive TEMP CatalogService navigation controls: " +
            "real Release/Disc ComboBox SelectionChanged events routed to real TableView, " +
            "3/2/2 rows, filter and back-to-releases, no production files";
    }
}
