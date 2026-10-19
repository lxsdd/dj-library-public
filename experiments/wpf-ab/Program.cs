using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DJLibrary.SyntheticComparison;

namespace DJLibrary.WpfAb;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--smoke", StringComparer.Ordinal))
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var window = new ComparisonWindow();
            window.Show();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            try
            {
                window.RunSyntheticSmoke();
            }
            catch (Exception e)
            {
                var diagnostics = Environment.GetEnvironmentVariable("DJLIB_WPF_AB_SMOKE_REPORT");
                if (!string.IsNullOrWhiteSpace(diagnostics))
                {
                    try { System.IO.File.WriteAllText(diagnostics, "FAIL\n" + e); }
                    catch { /* Preserve the original test failure. */ }
                }
                throw;
            }
            finally
            {
                window.Close();
                app.Shutdown();
            }
            return;
        }
        new Application().Run(new ComparisonWindow());
    }
}

internal sealed partial class ComparisonWindow : Window
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
        Width = 1000;
        Height = 590;
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

    internal void RunSyntheticSmoke()
    {
        var report = new List<string>();
        foreach (int size in SyntheticCatalog.SupportedSizes)
        {
            _size.SelectedIndex = Array.IndexOf(SyntheticCatalog.SupportedSizes, size);
            var samples = new List<double>();
            for (int repeat = 0; repeat < 5; repeat++)
            {
                Load(this, new RoutedEventArgs());
                // This is a compositor callback, NOT a physically presented frame.
                samples.Add(WaitForNextRenderingCallback());
            }
            samples.Sort();
            double frameMs = samples[2];
            double p95Ms = samples[4];
            if (_grid.ActualWidth < 50 || _grid.ActualHeight < 50)
                throw new InvalidOperationException("WPF DataGrid has no rendered layout");
            if (_grid.Items.Count != size) throw new InvalidOperationException("WPF DataGrid load count mismatch");
            _grid.SelectedItem = _all[0];
            _grid.SelectedItems.Add(_all[size / 2]);
            _search.Text = "Artist 0001";
            Filter(this, new RoutedEventArgs());
            if (_grid.Items.Count == 0) throw new InvalidOperationException("WPF DataGrid filter empty");
            Sort(this, new RoutedEventArgs());
            Clear(this, new RoutedEventArgs());
            if (_grid.Items.Count != size || _grid.SelectedItems.Count != 2)
                throw new InvalidOperationException("WPF DataGrid selection restore failed");
            // Explicit deselection must survive a round-trip through a restrictive filter.
            _grid.SelectedItems.Remove(_all[0]);
            _search.Text = "Artist 0001";
            Filter(this, new RoutedEventArgs());
            Clear(this, new RoutedEventArgs());
            if (_grid.SelectedItems.Count != 1 || !ReferenceEquals(_grid.SelectedItem, _all[size / 2]))
                throw new InvalidOperationException("WPF explicit deselection was not honored");
            _grid.ScrollIntoView(_all[^1]);
            _grid.UpdateLayout();
            using var currentProcess = Process.GetCurrentProcess();
            currentProcess.Refresh();
            var privateMiB = currentProcess.PrivateMemorySize64 / 1048576.0;
            var workingMiB = currentProcess.WorkingSet64 / 1048576.0;
            var outcome = $"PASS {size:N0} synthetic WPF DataGrid rows, real window layout " +
                $"{_grid.ActualWidth:F0}x{_grid.ActualHeight:F0}, next compositor Rendering callback " +
                $"median {frameMs:F1} ms, p95 sample {p95Ms:F1} ms (5 samples), " +
                $"process private {privateMiB:F1} MiB, working set {workingMiB:F1} MiB, " +
                "selection/filter/sort/scroll/explicit deselection";
            report.Add(outcome);
            Console.WriteLine(outcome);
        }

        // Match the WinUI TableView synthetic run: resize an already rendered
        // real HWND after filling 100,000 rows, and verify no identity loss.
        double oldWidth = Width, initialGrid = _grid.ActualWidth;
        var initialIds = _grid.SelectedItems.Cast<SyntheticTrackRow>()
            .Select(row => row.Identity).ToHashSet(StringComparer.Ordinal);
        double reducedGrid;
        try
        {
            Width = 850;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                _grid.Dispatcher.Invoke(() => { },
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                _grid.UpdateLayout();
                if (_grid.ActualWidth < initialGrid - 60) break;
            }
            reducedGrid = _grid.ActualWidth;
            if (reducedGrid >= initialGrid - 60)
                throw new InvalidOperationException(
                    $"WPF live HWND resize did not reduce DataGrid viewport: {initialGrid:F0}->{reducedGrid:F0}");
        }
        finally
        {
            Width = oldWidth;
            _grid.UpdateLayout();
            _grid.Dispatcher.Invoke(() => { },
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        if (Math.Abs(_grid.ActualWidth - initialGrid) > 5)
            throw new InvalidOperationException("WPF window viewport failed to restore after resize");
        var finalIds = _grid.SelectedItems.Cast<SyntheticTrackRow>()
            .Select(row => row.Identity).ToHashSet(StringComparer.Ordinal);
        if (!finalIds.SetEquals(initialIds))
            throw new InvalidOperationException("WPF live window resize changed selected track identities");
        string resizeReport =
            $"PASS WPF live window resize: DataGrid {initialGrid:F0}->{reducedGrid:F0}->{_grid.ActualWidth:F0}, " +
            "selected track identities preserved";
        report.Add(resizeReport);
        Console.WriteLine(resizeReport);

        // Both real Windows tables must expose navigable UI Automation Grid
        // and Selection providers, not merely attach an accessible name.
        var automationPeer = System.Windows.Automation.Peers.FrameworkElementAutomationPeer
            .CreatePeerForElement(_grid);
        if (automationPeer == null)
            throw new InvalidOperationException("WPF DataGrid automation peer missing");
        var gridProvider = automationPeer.GetPattern(
            System.Windows.Automation.Peers.PatternInterface.Grid)
            as System.Windows.Automation.Provider.IGridProvider;
        if (gridProvider == null ||
            gridProvider.RowCount != _grid.Items.Count ||
            gridProvider.ColumnCount != _grid.Columns.Count)
            throw new InvalidOperationException(
                "WPF DataGrid UIA Grid provider missing or shape incorrect");
        var selectionProvider = automationPeer.GetPattern(
            System.Windows.Automation.Peers.PatternInterface.Selection)
            as System.Windows.Automation.Provider.ISelectionProvider;
        if (selectionProvider == null)
            throw new InvalidOperationException("WPF DataGrid UIA Selection provider missing");
        var tableProvider = automationPeer.GetPattern(
            System.Windows.Automation.Peers.PatternInterface.Table)
            as System.Windows.Automation.Provider.ITableProvider;
        if (tableProvider == null)
            throw new InvalidOperationException("WPF DataGrid UIA Table provider missing");
        var columnHeaders = tableProvider.GetColumnHeaders();
        // Unlike TableView, stock WPF DataGrid's ITableProvider can return
        // null for GetColumnHeaders() even when column headers are visibly
        // rendered. Record this as a real baseline limitation; do not fake
        // parity or fail the unchanged production baseline for it.
        if (columnHeaders != null && columnHeaders.Length != gridProvider.ColumnCount)
            throw new InvalidOperationException(
                $"WPF DataGrid UIA column-header mismatch: " +
                $"got {columnHeaders.Length} / expected {gridProvider.ColumnCount}");
        string headerAssessment = columnHeaders == null
            ? "Table header provider enumeration unavailable (observed WPF baseline limitation)"
            : $"{columnHeaders.Length} Table column-header providers";
        string automationReport =
            $"PASS WPF accessibility: UIA Grid {gridProvider.RowCount}x{gridProvider.ColumnCount}, " +
            $"Selection and Table patterns; {headerAssessment} " +
            "(not physical screen-reader/keyboard acceptance)";
        report.Add(automationReport);
        Console.WriteLine(automationReport);

        string nativeReport = VerifyNativeCatalogBinding();
        report.Add(nativeReport);
        Console.WriteLine(nativeReport);
        string sqliteReport = VerifyDisposableSqliteBinding();
        report.Add(sqliteReport);
        Console.WriteLine(sqliteReport);

        var path = Environment.GetEnvironmentVariable("DJLIB_WPF_AB_SMOKE_REPORT");
        if (!string.IsNullOrWhiteSpace(path))
            System.IO.File.WriteAllText(path, "PASS\n" + string.Join("\n", report) +
                "\nNOT TESTED: actual monitor scanout, physical touchpad or visual frame pacing.");
    }

    private static void AddButton(Panel parent, string label, RoutedEventHandler click)
    {
        var button = new Button { Content = label, Margin = new Thickness(4, 0, 4, 0), Padding = new Thickness(8, 4, 8, 4) };
        button.Click += click;
        parent.Children.Add(button);
    }

    private void PreserveSelection()
    {
        // A user's explicit selection change replaces the remembered set.
        // Only invisible identities survive a filter; never re-add deselected visible rows.
        var visible = (_grid.ItemsSource?.Cast<SyntheticTrackRow>() ?? Enumerable.Empty<SyntheticTrackRow>())
            .Select(row => row.Identity).ToHashSet(StringComparer.Ordinal);
        _selectionIds.RemoveWhere(visible.Contains);
        foreach (SyntheticTrackRow row in _grid.SelectedItems.OfType<SyntheticTrackRow>())
            _selectionIds.Add(row.Identity);
    }

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
