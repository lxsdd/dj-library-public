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
        if (_navigation == null) return;
        NavigationReleaseSelector.SelectedIndex = -1;
        NavigationDiscSelector.SelectedIndex = -1;
        NavigationDiscSelector.ItemsSource = null;
        ShowNavigationRows(_navigation.Releases);
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
        if (_navigation.ForDiscEvidence(discId) is { } evidence)
            StatusText.Text = evidence.Description;
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
        if (!StatusText.Text.Contains("TOC: 150 12000 24000") ||
            !StatusText.Text.Contains("CD-TEXT: drive_present") ||
            !StatusText.Text.Contains("CDX: yes") ||
            !StatusText.Text.Contains("No physical CD drive accessed."))
            throw new InvalidOperationException("WinUI disc metadata detail not populated from original catalog");
        NavigationDiscSelector.SelectedItem = _navigation.ForDiscs(1)[1];
        await YieldToUiAsync();
        if (!StatusText.Text.Contains("TOC: 150 12500 24500") ||
            !StatusText.Text.Contains("CD-TEXT: drive_absent") ||
            StatusText.Text.Contains("TOC: 150 12000 24000"))
            throw new InvalidOperationException("WinUI displayed stale stored CD details after disc switch");
        NavigationDiscSelector.SelectedItem = _navigation.ForDiscs(1)[0];
        await YieldToUiAsync();

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
        if (StatusText.Text.Contains("TOC:"))
            throw new InvalidOperationException("WinUI stale CD details leaked into release view");
        NavigationReleaseSelector.SelectedItem = _navigation.Releases.Single(r => r.Identity == "release:2");
        await YieldToUiAsync();
        NavigationDiscSelector.SelectedItem = _navigation.ForDiscs(2)[0];
        await YieldToUiAsync();
        if (!StatusText.Text.Contains("TOC: unavailable") ||
            !StatusText.Text.Contains("CDX: unknown") ||
            !StatusText.Text.Contains("CD-TEXT: drive_unavailable"))
            throw new InvalidOperationException("WinUI unknown CD evidence was incorrectly promoted");
        NavigationReleaseSelector.SelectedItem = _navigation.Releases.Single(r => r.Identity == "release:3");
        await YieldToUiAsync();
        if (NavigationDiscSelector.Items.Count != 0 || NavigationDiscSelector.SelectedItem != null ||
            TracksGrid.Items.Count != 0 || StatusText.Text.Contains("TOC:"))
            throw new InvalidOperationException("WinUI empty release retained stale disc/track state");
        NavigationReleases_Click(this, new RoutedEventArgs());
        await YieldToUiAsync();
        if (NavigationReleaseSelector.SelectedItem != null || NavigationDiscSelector.SelectedItem != null ||
            NavigationDiscSelector.Items.Count != 0 || TracksGrid.Items.Count != 3 || StatusText.Text.Contains("TOC:"))
            throw new InvalidOperationException("WinUI returning to Releases retained stale hierarchy state");
        NavigationReleaseSelector.SelectedItem = _navigation.Releases.Single(r => r.Identity == "release:1");
        await YieldToUiAsync();
        NavigationDiscSelector.SelectedItem = _navigation.ForDiscs(1)[0];
        await YieldToUiAsync();
        if (TracksGrid.Items.Count != 2 || _all[0].Identity != "track:101")
            throw new InvalidOperationException("WinUI could not navigate after empty release");
        NavigationReleases_Click(this, new RoutedEventArgs());
        return "PASS WinUI interactive TEMP CatalogService navigation controls: " +
            "real Release/Disc ComboBox SelectionChanged events routed to real TableView, " +
            "3/2/2 rows, filter and back-to-releases, stored TOC/CD-TEXT/CDX detail transitions; no production files";
    }
}
