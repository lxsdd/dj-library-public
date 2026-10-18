using System.Diagnostics;
using DJLibrary.SyntheticComparison;
using Microsoft.UI.Xaml;

namespace DJLibrary.WinUiSpike;

public sealed partial class MainWindow : Window
{
    private List<SyntheticTrackRow> _all = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private static int RowCount(int selectedIndex) =>
        selectedIndex >= 0 && selectedIndex < SyntheticCatalog.SupportedSizes.Length
            ? SyntheticCatalog.SupportedSizes[selectedIndex] : 54_129;

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        var sw = Stopwatch.StartNew();
        _all = SyntheticCatalog.Generate(RowCount(SizeSelector.SelectedIndex));
        TracksGrid.ItemsSource = _all;
        sw.Stop();
        StatusText.Text = $"WinUI source + assignment: {sw.ElapsedMilliseconds} ms; " +
            $"rows: {_all.Count:N0}; identity hash: {SyntheticCatalog.Fingerprint(_all)}; " +
            $"managed heap: {GC.GetTotalMemory(false) / 1048576.0:F1} MiB. " +
            "Not a visible frame-time or WPF/WinUI performance conclusion.";
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        var sw = Stopwatch.StartNew();
        var visible = SyntheticCatalog.Filter(_all, SearchBox.Text);
        TracksGrid.ItemsSource = visible;
        sw.Stop();
        StatusText.Text = $"WinUI filter + assignment: {sw.ElapsedMilliseconds} ms; " +
            $"visible: {visible.Count:N0} / {_all.Count:N0}; " +
            $"identity hash: {SyntheticCatalog.Fingerprint(visible)}.";
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        var sw = Stopwatch.StartNew();
        var ordered = SyntheticCatalog.SortByTitle(TracksGrid.ItemsSource as IEnumerable<SyntheticTrackRow> ?? _all);
        TracksGrid.ItemsSource = ordered;
        sw.Stop();
        StatusText.Text = $"WinUI sort + assignment: {sw.ElapsedMilliseconds} ms; " +
            $"visible: {ordered.Count:N0}; identity hash: {SyntheticCatalog.Fingerprint(ordered)}.";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        TracksGrid.ItemsSource = _all;
        StatusText.Text = $"Showing all {_all.Count:N0} synthetic items.";
    }
}
