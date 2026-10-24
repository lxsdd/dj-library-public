using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DJLibrary.SyntheticComparison;

namespace DJLibrary.WpfAb;

internal sealed partial class ComparisonWindow
{
    private CatalogNavigationSnapshot? _navigation;
    private readonly ComboBox _navigationReleases = new()
        { Width = 185, DisplayMemberPath = "Title" };
    private readonly ComboBox _navigationDiscs = new()
        { Width = 145, DisplayMemberPath = "Title" };

    private StackPanel BuildNavigationToolbar()
    {
        var bar = new StackPanel { Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10) };
        bar.Children.Add(new TextBlock { Text = "TEMP SQLite navigation:",
            VerticalAlignment = VerticalAlignment.Center });
        AddButton(bar, "Load demo", NavigationLoad);
        AddButton(bar, "Releases", NavigationReleases);
        bar.Children.Add(new TextBlock { Text = "Release", Margin = new Thickness(6, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(_navigationReleases);
        bar.Children.Add(new TextBlock { Text = "Disc", Margin = new Thickness(6, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(_navigationDiscs);
        _navigationReleases.SelectionChanged += NavigationReleaseChanged;
        _navigationDiscs.SelectionChanged += NavigationDiscChanged;
        return bar;
    }

    private void ResetNavigationForSyntheticLoad()
    {
        System.Windows.Automation.AutomationProperties.SetName(_grid, "DJ Library synthetic track table");
        _navigation = null;
        _navigationReleases.ItemsSource = null;
        _navigationDiscs.ItemsSource = null;
    }

    private void ShowNavigationRows(System.Collections.Generic.List<SyntheticTrackRow> rows, string view)
    {
        PreserveSelection();
        _search.Text = "";
        _all = rows;
        _grid.ItemsSource = rows;
        RestoreSelection();
        System.Windows.Automation.AutomationProperties.SetName(_grid, "DJ Library temporary catalog " + view + " table");
        _status.Text = "Read-only TEMP CatalogService: " + rows.Count +
            " entries. No existing library, audio, settings or bridge accessed.";
    }

    private void NavigationLoad(object sender, RoutedEventArgs e)
    {
        _grid.SelectedItems.Clear();
        _selectionIds.Clear();
        _navigation = CatalogNavigationFixture.Load();
        _navigationDiscs.ItemsSource = null;
        _navigationReleases.ItemsSource = _navigation.Releases;
        _navigationReleases.SelectedIndex = -1;
        ShowNavigationRows(_navigation.Releases, "releases");
    }

    private void NavigationReleases(object sender, RoutedEventArgs e)
    {
        if (_navigation == null) return;
        _navigationReleases.SelectedIndex = -1;
        _navigationDiscs.SelectedIndex = -1;
        _navigationDiscs.ItemsSource = null;
        ShowNavigationRows(_navigation.Releases, "releases");
    }

    private void NavigationReleaseChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navigation == null || _navigationReleases.SelectedItem is not SyntheticTrackRow row ||
            !row.Identity.StartsWith("release:", StringComparison.Ordinal) ||
            !long.TryParse(row.Identity.Substring("release:".Length), out long releaseId))
            return;
        var discs = _navigation.ForDiscs(releaseId);
        _navigationDiscs.ItemsSource = discs;
        _navigationDiscs.SelectedIndex = -1;
        ShowNavigationRows(discs, "discs");
    }

    private void NavigationDiscChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navigation == null || _navigationDiscs.SelectedItem is not SyntheticTrackRow row ||
            !row.Identity.StartsWith("disc:", StringComparison.Ordinal) ||
            !long.TryParse(row.Identity.Substring("disc:".Length), out long discId))
            return;
        ShowNavigationRows(_navigation.ForTracks(discId), "tracks");
        if (_navigation.ForDiscEvidence(discId) is { } evidence)
            _status.Text = evidence.Description;
    }

    private string VerifyNavigationSelectors()
    {
        if (!_grid.IsReadOnly)
            throw new InvalidOperationException("WPF experimental DataGrid must remain read-only");
        NavigationLoad(this, new RoutedEventArgs());
        _grid.UpdateLayout();
        if (System.Windows.Automation.AutomationProperties.GetName(_grid) != "DJ Library temporary catalog releases table")
            throw new InvalidOperationException("WPF release table accessibility label missing");
        if (_navigation == null || _navigationReleases.Items.Count != 3 ||
            _grid.Items.Count != 3)
            throw new InvalidOperationException("WPF release selector is not bound");
        _navigationReleases.SelectedItem = _navigation.Releases.Single(r => r.Identity == "release:1");
        _grid.UpdateLayout();
        if (System.Windows.Automation.AutomationProperties.GetName(_grid) != "DJ Library temporary catalog discs table")
            throw new InvalidOperationException("WPF disc table accessibility label missing");
        if (_navigationDiscs.Items.Count != 2 || _grid.Items.Count != 2 ||
            _all[0].Identity != "disc:11")
            throw new InvalidOperationException("WPF release SelectionChanged not wired");
        _navigationDiscs.SelectedItem = _navigation.ForDiscs(1)[0];
        _grid.UpdateLayout();
        if (System.Windows.Automation.AutomationProperties.GetName(_grid) != "DJ Library temporary catalog tracks table")
            throw new InvalidOperationException("WPF track table accessibility label missing");
        if (_grid.Items.Count != 2 || _all[0].Identity != "track:101")
            throw new InvalidOperationException("WPF disc SelectionChanged not wired");
        if (!_status.Text.Contains("TOC: 150 12000 24000") ||
            !_status.Text.Contains("CD-TEXT: drive_present") ||
            !_status.Text.Contains("CDX: yes") ||
            !_status.Text.Contains("No physical CD drive accessed."))
            throw new InvalidOperationException("WPF disc metadata detail not populated from original catalog");
        _navigationDiscs.SelectedItem = _navigation.ForDiscs(1)[1];
        if (!_status.Text.Contains("TOC: 150 12500 24500") ||
            !_status.Text.Contains("CD-TEXT: drive_absent") ||
            _status.Text.Contains("TOC: 150 12000 24000"))
            throw new InvalidOperationException("WPF displayed stale stored CD details after disc switch");
        _navigationDiscs.SelectedItem = _navigation.ForDiscs(1)[0];

        _search.Text = "Opening";
        Filter(this, new RoutedEventArgs());
        if (_grid.Items.Count != 1) throw new InvalidOperationException("WPF navigation filtering failed");
        Clear(this, new RoutedEventArgs());
        NavigationReleases(this, new RoutedEventArgs());
        if (_grid.Items.Count != 3 || _grid.ActualWidth < 600)
            throw new InvalidOperationException("WPF release navigation restore failed");
        if (_status.Text.Contains("TOC:"))
            throw new InvalidOperationException("WPF stale CD details leaked into release view");
        _navigationReleases.SelectedItem = _navigation.Releases.Single(r => r.Identity == "release:2");
        _navigationDiscs.SelectedItem = _navigation.ForDiscs(2)[0];
        if (!_status.Text.Contains("TOC: unavailable") ||
            !_status.Text.Contains("CDX: unknown") ||
            !_status.Text.Contains("CD-TEXT: drive_unavailable"))
            throw new InvalidOperationException("WPF unknown CD evidence was incorrectly promoted");
        _navigationReleases.SelectedItem = _navigation.Releases.Single(r => r.Identity == "release:3");
        if (_navigationDiscs.Items.Count != 0 || _navigationDiscs.SelectedItem != null ||
            _grid.Items.Count != 0 || _status.Text.Contains("TOC:"))
            throw new InvalidOperationException("WPF empty release retained stale disc/track state");
        NavigationReleases(this, new RoutedEventArgs());
        if (_navigationReleases.SelectedItem != null || _navigationDiscs.SelectedItem != null ||
            _navigationDiscs.Items.Count != 0 || _grid.Items.Count != 3 || _status.Text.Contains("TOC:"))
            throw new InvalidOperationException("WPF returning to Releases retained stale hierarchy state");
        _navigationReleases.SelectedItem = _navigation.Releases.Single(r => r.Identity == "release:1");
        _navigationDiscs.SelectedItem = _navigation.ForDiscs(1)[0];
        if (_grid.Items.Count != 2 || _all[0].Identity != "track:101")
            throw new InvalidOperationException("WPF could not navigate after empty release");
        NavigationReleases(this, new RoutedEventArgs());
        return "PASS WPF interactive TEMP CatalogService navigation controls: " +
            "real Release/Disc ComboBox SelectionChanged events routed to real DataGrid, " +
            "3/2/2 rows, filter and back-to-releases, stored TOC/CD-TEXT/CDX detail transitions; no production files";
    }
}
