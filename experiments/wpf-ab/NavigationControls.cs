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
        _navigation = null;
        _navigationReleases.ItemsSource = null;
        _navigationDiscs.ItemsSource = null;
    }

    private void ShowNavigationRows(System.Collections.Generic.List<SyntheticTrackRow> rows)
    {
        PreserveSelection();
        _search.Text = "";
        _all = rows;
        _grid.ItemsSource = rows;
        RestoreSelection();
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
        ShowNavigationRows(_navigation.Releases);
    }

    private void NavigationReleases(object sender, RoutedEventArgs e)
    {
        if (_navigation != null) ShowNavigationRows(_navigation.Releases);
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
        ShowNavigationRows(discs);
    }

    private void NavigationDiscChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navigation == null || _navigationDiscs.SelectedItem is not SyntheticTrackRow row ||
            !row.Identity.StartsWith("disc:", StringComparison.Ordinal) ||
            !long.TryParse(row.Identity.Substring("disc:".Length), out long discId))
            return;
        ShowNavigationRows(_navigation.ForTracks(discId));
    }

    private string VerifyNavigationSelectors()
    {
        NavigationLoad(this, new RoutedEventArgs());
        _grid.UpdateLayout();
        if (_navigation == null || _navigationReleases.Items.Count != 3 ||
            _grid.Items.Count != 3)
            throw new InvalidOperationException("WPF release selector is not bound");
        _navigationReleases.SelectedItem = _navigation.Releases.Single(r => r.Identity == "release:1");
        _grid.UpdateLayout();
        if (_navigationDiscs.Items.Count != 2 || _grid.Items.Count != 2 ||
            _all[0].Identity != "disc:11")
            throw new InvalidOperationException("WPF release SelectionChanged not wired");
        _navigationDiscs.SelectedItem = _navigation.ForDiscs(1)[0];
        _grid.UpdateLayout();
        if (_grid.Items.Count != 2 || _all[0].Identity != "track:101")
            throw new InvalidOperationException("WPF disc SelectionChanged not wired");
        _search.Text = "Opening";
        Filter(this, new RoutedEventArgs());
        if (_grid.Items.Count != 1) throw new InvalidOperationException("WPF navigation filtering failed");
        Clear(this, new RoutedEventArgs());
        NavigationReleases(this, new RoutedEventArgs());
        if (_grid.Items.Count != 3 || _grid.ActualWidth < 600)
            throw new InvalidOperationException("WPF release navigation restore failed");
        return "PASS WPF interactive TEMP CatalogService navigation controls: " +
            "real Release/Disc ComboBox SelectionChanged events routed to real DataGrid, " +
            "3/2/2 rows, filter and back-to-releases, no production files";
    }
}
