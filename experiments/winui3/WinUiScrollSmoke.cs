using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DJLibrary.WinUiSpike;

public sealed partial class MainWindow
{
    private static void GatherScrollViewers(DependencyObject root, List<ScrollViewer> viewers)
    {
        if (root is ScrollViewer viewer) viewers.Add(viewer);
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
            GatherScrollViewers(VisualTreeHelper.GetChild(root, index), viewers);
    }

    private async Task<string> VerifyProgrammaticScrollContractAsync()
    {
        if (_all.Count != 100_000)
            throw new InvalidOperationException("Scroll regression must use 100,000 synthetic tracks");

        var originalWidths = TracksGrid.Columns.Select(x => x.Width).ToArray();
        var originalMinimums = TracksGrid.Columns.Select(x => x.MinWidth).ToArray();
        try
        {
            foreach (var column in TracksGrid.Columns)
            {
                column.MinWidth = 250;
                column.Width = new GridLength(250, GridUnitType.Pixel);
            }

            await AwaitActualFrameAsync();
            await YieldToUiAsync();

            var scrolls = new List<ScrollViewer>();
            GatherScrollViewers(TracksGrid, scrolls);
            // TableView's default inner ScrollViewer can disable the horizontal
            // mode even when columns require more width than the viewport.
            // Enable the actual rendered viewer rather than assuming column.Width
            // alone guarantees a usable horizontal scrolling surface.
            foreach (var rendered in scrolls)
            {
                rendered.HorizontalScrollMode = ScrollMode.Enabled;
                rendered.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            TracksGrid.UpdateLayout();
            await AwaitActualFrameAsync();
            await YieldToUiAsync();
            scrolls.Clear();
            GatherScrollViewers(TracksGrid, scrolls);
            ScrollViewer? horizontal = scrolls
                .Where(x => x.ScrollableWidth > 100)
                .OrderByDescending(x => x.ScrollableWidth)
                .FirstOrDefault();
            ScrollViewer? vertical = scrolls
                .Where(x => x.ScrollableHeight > 100)
                .OrderByDescending(x => x.ScrollableHeight)
                .FirstOrDefault();

            if (horizontal == null || vertical == null)
            {
                string diagnostics = string.Join("; ", scrolls.Select((s, index) =>
                    $"viewer[{index}] viewport={s.ViewportWidth:F0}x{s.ViewportHeight:F0} " +
                    $"extent={s.ExtentWidth:F0}x{s.ExtentHeight:F0} " +
                    $"scrollable={s.ScrollableWidth:F0}x{s.ScrollableHeight:F0} " +
                    $"offset={s.HorizontalOffset:F0},{s.VerticalOffset:F0}"));
                throw new InvalidOperationException(
                    "Real TableView did not expose both scroll directions after forcing " +
                    $"250-pixel columns; actual column widths=" +
                    string.Join(",", TracksGrid.Columns.Select(c => c.ActualWidth.ToString("F0"))) +
                    $"; {scrolls.Count} viewer(s): {diagnostics}");
            }

            // A DJ user resizes a column while the grid is already visible.
            // The horizontal viewport must grow immediately, not only after a
            // complete TableView rebuild or application restart.
            double beforeResize = horizontal.ScrollableWidth;
            double oldWidth = TracksGrid.Columns[0].Width.Value;
            TracksGrid.Columns[0].Width = new GridLength(oldWidth + 120, GridUnitType.Pixel);
            // WinUI.TableView's own live/preview resize code commits BOTH
            // ActualWidth and Width. Calling only Width is not a supported
            // equivalent of its realized-column resize path.
            TracksGrid.Columns[0].ActualWidth = oldWidth + 120;
            // Re-measure after programmatic column resizing. Never relax the extent gate.
            horizontal.InvalidateMeasure();
            TracksGrid.InvalidateMeasure();
            TracksGrid.UpdateLayout();
            await AwaitActualFrameAsync();
            await YieldToUiAsync();
            double afterResize = horizontal.ScrollableWidth;
            // WinUI's TableView column realization and its ScrollViewer extent
            // can settle on different compositor/dispatcher passes. Give the
            // actual HWND bounded frame opportunities rather than asserting
            // the first frame must already contain the new extent.
            for (int pass = 0; pass < 12 && afterResize < beforeResize + 60; pass++)
            {
                await YieldToUiAsync();
                TracksGrid.UpdateLayout();
                await AwaitActualFrameAsync();
                afterResize = horizontal.ScrollableWidth;
            }
            if (afterResize < beforeResize + 60)
                throw new InvalidOperationException(
                    $"Live column resize failed to expand horizontal scroll range: " +
                    $"before={beforeResize:F0}, after={afterResize:F0}, " +
                    $"measured column={TracksGrid.Columns[0].ActualWidth:F0}, " +
                    $"specified={TracksGrid.Columns[0].Width.Value:F0}, " +
                    $"extent={horizontal.ExtentWidth:F0}, viewport={horizontal.ViewportWidth:F0}, " +
                    $"column actual total={TracksGrid.Columns.Sum(c => c.ActualWidth):F0}; " +
                    string.Join("; ", scrolls.Select((v, i) =>
                        $"scroll[{i}]: extent={v.ExtentWidth:F0} viewport={v.ViewportWidth:F0} " +
                        $"scrollable={v.ScrollableWidth:F0} hOffset={v.HorizontalOffset:F0}")));

            horizontal.ChangeView(120, null, null, true);
            await AwaitActualFrameAsync();
            if (horizontal.HorizontalOffset < 60)
                throw new InvalidOperationException(
                    "WinUI horizontal scrolling offset did not move right: " +
                    horizontal.HorizontalOffset);

            horizontal.ChangeView(horizontal.ScrollableWidth, null, null, true);
            await AwaitActualFrameAsync();
            if (horizontal.HorizontalOffset < horizontal.ScrollableWidth - 2)
                throw new InvalidOperationException(
                    "WinUI cannot reach last column at horizontal right edge");

            double startingVertical = vertical.VerticalOffset;
            TracksGrid.ScrollIntoView(_all[^1]);
            await AwaitActualFrameAsync();
            await YieldToUiAsync();
            if (vertical.VerticalOffset <= startingVertical + 10)
                throw new InvalidOperationException(
                    "WinUI cannot programmatically scroll to the final synthetic track");

            // The logical UIA grid provider must also expose *realized* cells
            // in a virtualized 100k-row/6-column model. This does not assert
            // all 600k unmaterialized cells exist simultaneously in memory.
            var automationPeer =
                Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer
                    .CreatePeerForElement(TracksGrid);
            var gridProvider = automationPeer?.GetPattern(
                Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Grid) as
                Microsoft.UI.Xaml.Automation.Provider.IGridProvider;
            if (gridProvider == null || gridProvider.RowCount != _all.Count ||
                gridProvider.ColumnCount != TracksGrid.Columns.Count)
                throw new InvalidOperationException("WinUI virtualized UIA Grid provider has wrong logical shape");

            bool finalCell = false;
            for (int pass = 0; pass < 8; pass++)
            {
                if (gridProvider.GetItem(_all.Count - 1, TracksGrid.Columns.Count - 1) != null)
                {
                    finalCell = true;
                    break;
                }
                await YieldToUiAsync();
                await AwaitActualFrameAsync();
            }
            if (!finalCell)
                throw new InvalidOperationException(
                    "WinUI UIA Grid did not expose the final realized row/column " +
                    "after right-edge and last-row ScrollIntoView");

            horizontal.ChangeView(0, null, null, true);
            TracksGrid.ScrollIntoView(_all[0]);
            await AwaitActualFrameAsync();
            await YieldToUiAsync();
            bool firstCell = false;
            for (int pass = 0; pass < 8; pass++)
            {
                if (gridProvider.GetItem(0, 0) != null)
                {
                    firstCell = true;
                    break;
                }
                await YieldToUiAsync();
                await AwaitActualFrameAsync();
            }
            if (!firstCell)
                throw new InvalidOperationException(
                    "WinUI UIA Grid did not expose first realized row/column " +
                    "after return to top-left");

            // Live window resizing is independent from column resizing and
            // indispensable for large custom DJ tables. Verify the actual
            // visible TableView viewport/scroll extent and stable identities.
            var preservedIds = TracksGrid.SelectedItems
                .OfType<DJLibrary.SyntheticComparison.SyntheticTrackRow>()
                .Select(row => row.Identity).ToHashSet(StringComparer.Ordinal);
            int originalWidth = AppWindow.Size.Width;
            int originalHeight = AppWindow.Size.Height;
            double originalGrid = TracksGrid.ActualWidth;
            double originalHorizontal = horizontal.ScrollableWidth;
            double narrowedGrid = originalGrid, narrowedOverflow = originalHorizontal;
            try
            {
                AppWindow.Resize(new Windows.Graphics.SizeInt32(850, originalHeight));
                for (int pass = 0; pass < 12; pass++)
                {
                    await YieldToUiAsync();
                    TracksGrid.UpdateLayout();
                    await AwaitActualFrameAsync();
                    narrowedGrid = TracksGrid.ActualWidth;
                    narrowedOverflow = horizontal.ScrollableWidth;
                    if (narrowedGrid < originalGrid - 60 &&
                        narrowedOverflow > originalHorizontal + 50) break;
                }
                if (narrowedGrid >= originalGrid - 60 ||
                    narrowedOverflow <= originalHorizontal + 50)
                    throw new InvalidOperationException(
                        $"WinUI live outer-window resize did not adjust rendered TableView/scroll range: " +
                        $"grid={originalGrid:F0}->{narrowedGrid:F0}, " +
                        $"horizontal={originalHorizontal:F0}->{narrowedOverflow:F0}, " +
                        $"actualWindowWidth={AppWindow.Size.Width}");

                AppWindow.Resize(new Windows.Graphics.SizeInt32(originalWidth, originalHeight));
                double restoredGrid = narrowedGrid;
                double restoredOverflow = narrowedOverflow;
                for (int pass = 0; pass < 12; pass++)
                {
                    await YieldToUiAsync();
                    TracksGrid.UpdateLayout();
                    await AwaitActualFrameAsync();
                    restoredGrid = TracksGrid.ActualWidth;
                    restoredOverflow = horizontal.ScrollableWidth;
                    if (Math.Abs(restoredGrid - originalGrid) < 5 &&
                        Math.Abs(restoredOverflow - originalHorizontal) < 8) break;
                }
                if (Math.Abs(restoredGrid - originalGrid) >= 5 ||
                    Math.Abs(restoredOverflow - originalHorizontal) >= 8)
                    throw new InvalidOperationException(
                        $"WinUI window resize restoration lost viewport/scroll: " +
                        $"grid={originalGrid:F0}->{restoredGrid:F0}, " +
                        $"horizontal={originalHorizontal:F0}->{restoredOverflow:F0}");

                var selectedAfterResize = TracksGrid.SelectedItems
                    .OfType<DJLibrary.SyntheticComparison.SyntheticTrackRow>()
                    .Select(row => row.Identity).ToHashSet(StringComparer.Ordinal);
                if (!selectedAfterResize.SetEquals(preservedIds))
                    throw new InvalidOperationException(
                        "WinUI live window resize changed selected track identities");
            }
            finally
            {
                AppWindow.Resize(new Windows.Graphics.SizeInt32(originalWidth, originalHeight));
            }

            return "PASS WinUI scrolling: rendered horizontal overflow, offsets/right edge, " +
                "ScrollIntoView(final of 100,000 tracks), vertical offset advanced " +
                $"(horizontal max {horizontal.ScrollableWidth:F0}; vertical {vertical.VerticalOffset:F0}); " +
                $"PASS WinUI live window resize: viewport {originalGrid:F0}->{narrowedGrid:F0}->{TracksGrid.ActualWidth:F0}, " +
                "horizontal extent tracks the window and selected identities preserved";
        }
        finally
        {
            for (int i = 0; i < originalWidths.Length; i++)
                {
                    TracksGrid.Columns[i].MinWidth = originalMinimums[i];
                    TracksGrid.Columns[i].Width = originalWidths[i];
                }
        }
    }
}
