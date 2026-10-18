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
                    $"specified={TracksGrid.Columns[0].Width.Value:F0}");

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

            return "PASS WinUI scrolling: rendered horizontal overflow, offsets/right edge, " +
                "ScrollIntoView(final of 100,000 tracks), vertical offset advanced " +
                $"(horizontal max {horizontal.ScrollableWidth:F0}; vertical {vertical.VerticalOffset:F0})";
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
