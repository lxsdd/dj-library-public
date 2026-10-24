using System;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;

namespace DJLibrary.WpfAb;

internal sealed partial class ComparisonWindow
{
    private static double WaitForNextRenderingCallback()
    {
        var frame = new DispatcherFrame();
        var clock = Stopwatch.StartNew();
        bool observed = false;
        double milliseconds = 0;
        EventHandler? rendering = null;
        DispatcherTimer? timeout = null;
        rendering = (sender, args) =>
        {
            observed = true;
            milliseconds = clock.Elapsed.TotalMilliseconds;
            frame.Continue = false;
        };
        timeout = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(8)
        };
        timeout.Tick += (sender, args) => { frame.Continue = false; };
        CompositionTarget.Rendering += rendering;
        timeout.Start();
        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timeout.Stop();
            CompositionTarget.Rendering -= rendering;
        }
        if (!observed)
            throw new TimeoutException("WPF compositor produced no Rendering callback after ItemsSource assignment");
        return milliseconds;
    }
}
