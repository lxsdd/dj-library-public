// Synthetic-only WPF keyboard comparator in the real Windows CI desktop.
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DJLibrary.WpfAb;

internal sealed partial class ComparisonWindow
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern void keybd_event(
        byte keyCode, byte scanCode, uint flags, UIntPtr extraInfo);

    private static bool WithinGrid(object? focus, DependencyObject table)
    {
        var node = focus as DependencyObject;
        for (int hops = 0; node != null && hops < 100; hops++)
        {
            if (ReferenceEquals(node, table)) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private static void PumpKeyboardFor(TimeSpan interval)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
            { Interval = interval };
        timer.Tick += (sender, args) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private string ProbeKeyboardFocusEscape()
    {
        const string marker = "KEYBOARD_FOCUS_OBSERVATION WPF baseline: ";
        if (_all.Count != 100_000 || _grid.Items.Count != 100_000)
            throw new InvalidOperationException("WPF keyboard comparator needs 100k rows");
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return marker + "INCONCLUSIVE missing HWND";
        SetForegroundWindow(hwnd);
        PumpKeyboardFor(TimeSpan.FromMilliseconds(80));
        if (GetForegroundWindow() != hwnd)
            return marker + "INCONCLUSIVE Windows runner input desktop unavailable";

        _grid.ScrollIntoView(_all[0]);
        _grid.UpdateLayout();
        _grid.Focus();
        Keyboard.Focus(_grid);
        PumpKeyboardFor(TimeSpan.FromMilliseconds(80));
        if (!WithinGrid(Keyboard.FocusedElement, _grid))
            return marker + "INCONCLUSIVE table focus setup failed";

        int delivered = 0;
        KeyEventHandler observer = (sender, args) =>
        {
            if (args.Key == Key.Tab) delivered++;
        };
        _grid.AddHandler(Keyboard.KeyDownEvent, observer, true);
        try
        {
            for (int n = 1; n <= 24; n++)
            {
                keybd_event(0x09, 0, 0, UIntPtr.Zero);
                keybd_event(0x09, 0, 0x0002, UIntPtr.Zero);
                PumpKeyboardFor(TimeSpan.FromMilliseconds(75));
                if (GetForegroundWindow() != hwnd)
                    return marker + "INCONCLUSIVE foreground changed";
                if (!WithinGrid(Keyboard.FocusedElement, _grid))
                    return marker + (delivered > 0 ? "PASS" : "INCONCLUSIVE") +
                        " Tab escaped WPF DataGrid after " + n + " press(es); " +
                        "Tab routed events received=" + delivered;
            }
            if (delivered == 0)
                return marker + "INCONCLUSIVE no Tab events received by WPF DataGrid";
            return marker + "IN_TABLE_FORWARD_24 WPF DataGrid retained focus after 24 Tab presses; " +
                "Tab routed events received=" + delivered;
        }
        finally
        {
            _grid.RemoveHandler(Keyboard.KeyDownEvent, observer);
        }
    }
    // Compare *focus retention outside* the table, independent of the number
    // of Tab cells in a 100k-row grid. This is not human keyboard acceptance.
    private string ProbeExplicitFocusTransfer()
    {
        const string marker = "FOCUS_TRANSFER_OBSERVATION WPF baseline: ";
        if (_all.Count != 100_000) return marker + "INCONCLUSIVE wrong fixture";
        _grid.ScrollIntoView(_all[0]);
        _grid.UpdateLayout();
        Keyboard.Focus(_grid);
        PumpKeyboardFor(TimeSpan.FromMilliseconds(80));
        if (!WithinGrid(Keyboard.FocusedElement, _grid))
            return marker + "INCONCLUSIVE table focus not established";
        bool requested = _focusProbeTarget.Focus();
        Keyboard.Focus(_focusProbeTarget);
        PumpKeyboardFor(TimeSpan.FromMilliseconds(180));
        return marker + (requested && ReferenceEquals(Keyboard.FocusedElement, _focusProbeTarget)
            ? "PASS target button retained keyboard focus after WPF DataGrid"
            : "BLOCKED focused control reverted or target focus was refused");
    }

    // Reverse traversal from the visual start tests whether keyboard users can
    // reach earlier toolbar controls; this is stronger than 24 forward Tabs in 100k rows.
    private string ProbeReverseKeyboardBoundary()
    {
        const string marker = "REVERSE_TAB_OBSERVATION WPF baseline: ";
        if (_all.Count != 100_000 || _grid.Items.Count != 100_000)
            return marker + "INCONCLUSIVE wrong fixture";
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        SetForegroundWindow(hwnd);
        PumpKeyboardFor(TimeSpan.FromMilliseconds(80));
        if (GetForegroundWindow() != hwnd)
            return marker + "INCONCLUSIVE foreground desktop unavailable";
        _grid.ScrollIntoView(_all[0]);
        _grid.UpdateLayout();
        Keyboard.Focus(_grid);
        PumpKeyboardFor(TimeSpan.FromMilliseconds(80));
        if (!WithinGrid(Keyboard.FocusedElement, _grid))
            return marker + "INCONCLUSIVE initial table keyboard focus unavailable";

        int delivered = 0;
        KeyEventHandler observer = (sender, args) => {
            if (args.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) delivered++;
        };
        _grid.AddHandler(Keyboard.KeyDownEvent, observer, true);
        try
        {
            for (int press = 1; press <= 8; press++)
            {
                keybd_event(0x10, 0, 0, UIntPtr.Zero); // Shift down
                try
                {
                    keybd_event(0x09, 0, 0, UIntPtr.Zero);
                    keybd_event(0x09, 0, 0x0002, UIntPtr.Zero);
                }
                finally { keybd_event(0x10, 0, 0x0002, UIntPtr.Zero); }
                PumpKeyboardFor(TimeSpan.FromMilliseconds(100));
                if (GetForegroundWindow() != hwnd)
                    return marker + "INCONCLUSIVE foreground changed";
                if (!WithinGrid(Keyboard.FocusedElement, _grid))
                    return marker + (delivered > 0 ? "PASS" : "INCONCLUSIVE") +
                        " escaped backward after " + press + " Shift+Tab event(s), delivered=" + delivered;
            }
            return marker + (delivered > 0 ? "IN_TABLE_REVERSE_8" : "INCONCLUSIVE") +
                " after 8 Shift+Tab presses, delivered=" + delivered +
                "; verify initial focused cell and boundary on physical keyboard";
        }
        finally { _grid.RemoveHandler(Keyboard.KeyDownEvent, observer); }
    }

}
