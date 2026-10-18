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
        try
        {
            foreach (var column in TracksGrid.Columns)
                column.Width = new GridLength(250, GridUnitType.Pixel);

            await AwaitActualFrameAsync();
            await YieldToUiAsync();

            var scrolls = new List<ScrollViewer>();
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
                    $"250-pixel columns; {scrolls.Count} viewer(s): {diagnostics}");
            }

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
                TracksGrid.Columns[i].Width = originalWidths[i];
        }
    }
}
