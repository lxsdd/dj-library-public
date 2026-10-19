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
            var samples = new List<double>();
            for (int repeat = 0; repeat < 5; repeat++)
            {
                Load_Click(this, new RoutedEventArgs());
                samples.Add(await AwaitActualFrameAsync());
            }
            samples.Sort();
            double frameMs = samples[2];
            double p95Ms = samples[4];
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

            using var currentProcess = Process.GetCurrentProcess();
            currentProcess.Refresh();
            var privateMiB = currentProcess.PrivateMemorySize64 / 1048576.0;
            var workingMiB = currentProcess.WorkingSet64 / 1048576.0;
            evidence.Add(
                $"PASS {size:N0} synthetic WinUI TableView rows, real window layout " +
                $"{TracksGrid.ActualWidth:F0}x{TracksGrid.ActualHeight:F0}, " +
                $"next compositor callback median {frameMs:F1} ms, " +
                $"p95 sample {p95Ms:F1} ms (5 samples), " +
                $"process private {privateMiB:F1} MiB, working set {workingMiB:F1} MiB, " +
                $"filter {filteredCount:N0}, sort, selection/restoration, explicit deselection");
        }
        // Programmatic column state is a prerequisite to preserving the
        // user's named grid layout, not a claim of end-user drag/drop parity.
        evidence.Add(VerifySyntheticColumnStateRoundTrip());
        evidence.Add(VerifyTableAccessibilityContract());
        evidence.Add(await VerifyProgrammaticScrollContractAsync());
        evidence.Add(await VerifyNativeCatalogBindingAsync());
        evidence.Add(await VerifyDisposableSqliteBindingAsync());
        return string.Join("\n", evidence) +
            "\nNOT TESTED: user drag gestures, native horizontal touchpad, real settings path, " +
            "monitor scanout, packaging, user catalog, mixed-DPI, accessibility or visual parity.";
    }
}
