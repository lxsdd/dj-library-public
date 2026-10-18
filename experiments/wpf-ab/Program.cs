using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DJLibrary.SyntheticComparison;

namespace DJLibrary.WpfAb;

internal static class Program
{
    [STAThread]
    public static void Main() => new Application().Run(new ComparisonWindow());
}

internal sealed class ComparisonWindow : Window
{
    private readonly ComboBox _size = new ComboBox { Width = 100, SelectedIndex = 2 };
    private readonly TextBox _search = new TextBox { Width = 260, Margin = new Thickness(4, 0, 4, 0) };
    private readonly TextBlock _status = new TextBlock { Text = "No synthetic data loaded.", TextWrapping = TextWrapping.Wrap };
    private readonly DataGrid _grid = new DataGrid
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserSortColumns = true,
        SelectionMode = DataGridSelectionMode.Extended,
        SelectionUnit = DataGridSelectionUnit.FullRow,
        EnableRowVirtualization = true,
        EnableColumnVirtualization = true
    };
    private List<SyntheticTrackRow> _all = new();
    private readonly HashSet<string> _selectionIds = new(StringComparer.Ordinal);

    public ComparisonWindow()
    {
        Title = "DJ Library — WPF .NET 10 synthetic A/B workspace";
        Width = 1200;
        Height = 780;
        MinWidth = 700;
        MinHeight = 400;
        foreach (int count in SyntheticCatalog.SupportedSizes) _size.Items.Add(count.ToString("N0"));
        _size.SelectedIndex = 2;
        foreach (string property in new[] { "Identity", "Artist", "Title", "Genre", "Year", "Duration" })
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = property,
                Binding = new Binding(property),
                Width = new DataGridLength(160)
            });
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        bar.Children.Add(new TextBlock { Text = "Synthetic rows: ", VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(_size);
        AddButton(bar, "Generate / load", Load);
        bar.Children.Add(_search);
        AddButton(bar, "Apply filter", Filter);
        AddButton(bar, "Sort by title", Sort);
        AddButton(bar, "Show all", Clear);
        Grid.SetRow(bar, 0);
        Grid.SetRow(_grid, 1);
        _status.Margin = new Thickness(0, 8, 0, 0);
        Grid.SetRow(_status, 2);
        root.Children.Add(bar);
        root.Children.Add(_grid);
        root.Children.Add(_status);
        Content = root;
    }

    private static void AddButton(Panel parent, string label, RoutedEventHandler click)
    {
        var button = new Button { Content = label, Margin = new Thickness(4, 0, 4, 0), Padding = new Thickness(8, 4, 8, 4) };
        button.Click += click;
        parent.Children.Add(button);
    }

    private void PreserveSelection() { foreach (SyntheticTrackRow row in _grid.SelectedItems.OfType<SyntheticTrackRow>()) _selectionIds.Add(row.Identity); }

    private void RestoreSelection()
    {
        _grid.SelectedItems.Clear();
        foreach (var row in SyntheticSelection.Restore(_grid.ItemsSource?.Cast<SyntheticTrackRow>() ?? Enumerable.Empty<SyntheticTrackRow>(), _selectionIds))
            _grid.SelectedItems.Add(row);
    }

    private void Load(object sender, RoutedEventArgs e)
    {
        var timer = Stopwatch.StartNew();
        _selectionIds.Clear();
        _all = SyntheticCatalog.Generate(SyntheticCatalog.SupportedSizes[_size.SelectedIndex]);
        _grid.ItemsSource = _all;
        timer.Stop();
        Report("WPF source + assignment", timer, _all);
    }

    private void Filter(object sender, RoutedEventArgs e)
    {
        var timer = Stopwatch.StartNew();
        PreserveSelection();
        var rows = SyntheticCatalog.Filter(_all, _search.Text);
        _grid.ItemsSource = rows;
        RestoreSelection();
        timer.Stop();
        Report("WPF filter + assignment", timer, rows);
    }

    private void Sort(object sender, RoutedEventArgs e)
    {
        var timer = Stopwatch.StartNew();
        PreserveSelection();
        var rows = SyntheticCatalog.SortByTitle(_grid.ItemsSource?.Cast<SyntheticTrackRow>() ?? _all);
        _grid.ItemsSource = rows;
        RestoreSelection();
        timer.Stop();
        Report("WPF sort + assignment", timer, rows);
    }

    private void Clear(object sender, RoutedEventArgs e)
    {
        PreserveSelection();
        _search.Clear();
        _grid.ItemsSource = _all;
        RestoreSelection();
        _status.Text = $"Showing all {_all.Count:N0} synthetic rows.";
    }

    private void Report(string operation, Stopwatch timer, List<SyntheticTrackRow> rows)
    {
        _status.Text = $"{operation}: {timer.ElapsedMilliseconds} ms; " +
            $"visible: {rows.Count:N0} / {_all.Count:N0}; identity hash: {SyntheticCatalog.Fingerprint(rows)}; " +
            $"managed heap: {GC.GetTotalMemory(false) / 1048576.0:F1} MiB. " +
            "Not a visible frame-time or WinUI/WPF performance conclusion.";
    }
}
