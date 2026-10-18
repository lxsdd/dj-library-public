using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DJLibrary.SyntheticComparison;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DJLibrary.WinUiSpike;

public sealed partial class MainWindow
{
    private async Task<double> AwaitActualFrameAsync()
    {
        // Unlike the existing ItemsSource stopwatch, this awaits the WinUI
        // compositor's *next* Rendering callback with the real window open.
        // This is a frame-submission signal, not proof of a frame displayed
        // physically on a user's monitor or of frame pacing.
        var completion = new TaskCompletionSource<double>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = Stopwatch.StartNew();
        EventHandler<object>? rendering = null;
        rendering = (sender, args) =>
        {
            CompositionTarget.Rendering -= rendering;
            completion.TrySetResult(clock.Elapsed.TotalMilliseconds);
        };
        CompositionTarget.Rendering += rendering;
        try
        {
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
        }
        finally
        {
            CompositionTarget.Rendering -= rendering;
        }
    }

    private async Task YieldToUiAsync()
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low,
                () => completion.TrySetResult(true)))
            throw new InvalidOperationException("WinUI dispatcher is unavailable");
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
    }

    private static int CountRows(object? rows) =>
        (rows as IReadOnlyCollection<SyntheticTrackRow>)?.Count
        ?? (rows as IEnumerable<SyntheticTrackRow>)?.Count() ?? -1;

    internal async Task<string> RunSyntheticSmokeAsync()
    {
        var evidence = new List<string>();
        await YieldToUiAsync();
        foreach (int size in SyntheticCatalog.SupportedSizes)
        {
            SizeSelector.SelectedIndex = Array.IndexOf(SyntheticCatalog.SupportedSizes, size);
            Load_Click(this, new RoutedEventArgs());
            var frameMs = await AwaitActualFrameAsync();
            await YieldToUiAsync();

            if (TracksGrid.ActualWidth < 700 || TracksGrid.ActualHeight < 400)
                throw new InvalidOperationException(
                    $"WinUI TableView did not become laid out at {size:N0} rows: " +
                    $"{TracksGrid.ActualWidth}x{TracksGrid.ActualHeight}");
            if (CountRows(TracksGrid.ItemsSource) != size)
                throw new InvalidOperationException(
                    $"WinUI visible TableView ItemsSource count mismatch at {size:N0}");

            // Same identity-based scenarios as the WPF A/B reference window.
            TracksGrid.SelectedItems.Clear();
            TracksGrid.SelectedItems.Add(_all[0]);
            TracksGrid.SelectedItems.Add(_all[size / 2]);
            SearchBox.Text = "Artist 0001";
            Filter_Click(this, new RoutedEventArgs());
            await YieldToUiAsync();
            int filteredCount = CountRows(TracksGrid.ItemsSource);
            if (filteredCount == 0 || filteredCount >= size)
                throw new InvalidOperationException("WinUI filter did not reduce synthetic rows");

            Sort_Click(this, new RoutedEventArgs());
            Clear_Click(this, new RoutedEventArgs());
            await YieldToUiAsync();
            var selected = TracksGrid.SelectedItems.OfType<SyntheticTrackRow>()
                .Select(row => row.Identity).ToHashSet(StringComparer.Ordinal);
            if (CountRows(TracksGrid.ItemsSource) != size ||
                !selected.SetEquals(new[] { _all[0].Identity, _all[size / 2].Identity }))
                throw new InvalidOperationException(
                    $"WinUI selection lost row identities on filter/sort/restore at {size:N0}: " +
                    string.Join(",", selected));

            // A user removing a visible selection must not be undone by a
            // subsequent search reset or by an old invisible-row snapshot.
            TracksGrid.SelectedItems.Remove(_all[0]);
            SearchBox.Text = "Artist 0001";
            Filter_Click(this, new RoutedEventArgs());
            Clear_Click(this, new RoutedEventArgs());
            selected = TracksGrid.SelectedItems.OfType<SyntheticTrackRow>()
                .Select(row => row.Identity).ToHashSet(StringComparer.Ordinal);
            if (!selected.SetEquals(new[] { _all[size / 2].Identity }))
                throw new InvalidOperationException(
                    $"WinUI explicit deselection was not preserved at {size:N0}: " +
                    string.Join(",", selected));

            evidence.Add(
                $"PASS {size:N0} synthetic WinUI TableView rows, real window layout " +
                $"{TracksGrid.ActualWidth:F0}x{TracksGrid.ActualHeight:F0}, " +
                $"next compositor Rendering callback {frameMs:F1} ms, " +
                $"filter {filteredCount:N0}, sort, selection/restoration, explicit deselection");
        }
        return string.Join("\n", evidence) +
            "\nNOT TESTED: monitor display scanout, actual touchpad, packaging, user catalog, " +
            "multi-monitor DPI, accessibility or visual parity.";
    }
}
