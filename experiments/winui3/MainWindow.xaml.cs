using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace DJLibrary.WinUiSpike;

public sealed partial class MainWindow : Window
{
    private List<TrackRow> _all = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private static int RowCount(int selectedIndex) => selectedIndex switch
    {
        0 => 1_345,
        1 => 15_000,
        2 => 54_129,
        3 => 100_000,
        _ => 54_129
    };

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        var sw = Stopwatch.StartNew();
        var count = RowCount(SizeSelector.SelectedIndex);
        // Stable semantic identities. No collection data from the user's catalog.
        var generated = new List<TrackRow>(count);
        for (var n = 1; n <= count; n++)
        {
            generated.Add(new TrackRow(
                "synthetic-" + n.ToString("D7"),
                "Artist " + (n % 1100).ToString("D4"),
                "Track " + n.ToString("D6"),
                n % 4 switch { 0 => "House", 1 => "Trance", 2 => "Techno", _ => "Other" },
                (1990 + n % 37).ToString(),
                $"{3 + n % 7:00}:{n % 60:00}"));
        }
        _all = generated;
        TracksGrid.ItemsSource = _all;
        sw.Stop();
        StatusText.Text = $"Source model + assignment: {sw.ElapsedMilliseconds} ms; " +
            $"rows: {_all.Count:N0}; managed heap: {GC.GetTotalMemory(false) / 1048576.0:F1} MiB. " +
            "Not a UI frame-time or final WPF/WinUI performance claim.";
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        var query = (SearchBox.Text ?? string.Empty).Trim();
        var sw = Stopwatch.StartNew();
        var visible = query.Length == 0
            ? _all
            : _all.Where(x => x.Artist.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || x.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        TracksGrid.ItemsSource = visible;
        sw.Stop();
        StatusText.Text = $"Filtered {visible.Count:N0} / {_all.Count:N0} synthetic items " +
            $"in {sw.ElapsedMilliseconds} ms (query/filter + assignment only).";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        TracksGrid.ItemsSource = _all;
        StatusText.Text = $"Showing all {_all.Count:N0} synthetic items.";
    }
}

public sealed record TrackRow(
    string Identity,
    string Artist,
    string Title,
    string Genre,
    string Year,
    string Duration);
