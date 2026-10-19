using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DJLibrary.WinUiSpike;

public sealed partial class MainWindow
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern void keybd_event(
        byte keyCode, byte scanCode, uint flags, UIntPtr extraInfo);

    private static bool HasTableAncestor(object? focused, DependencyObject table)
    {
        var node = focused as DependencyObject;
        for (int hops = 0; node != null && hops < 100; hops++)
        {
            if (ReferenceEquals(node, table)) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    // Observational desktop probe only: this does not replace human keyboard,
    // narrator, accessibility or input-to-photon validation. No file writes.
    private async Task<string> ProbeKeyboardFocusEscapeAsync()
    {
        const string marker = "KEYBOARD_FOCUS_OBSERVATION WinUI v1.5.1: ";
        if (_all.Count != 100_000 || TracksGrid.Items.Count != 100_000)
            throw new InvalidOperationException("Keyboard probe requires the 100,000-row TableView");

        // CI hosts can expose a real HWND without an active input desktop.
        // They must never be recorded as proof that Tab works.
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero) return marker + "INCONCLUSIVE no active HWND";
        SetForegroundWindow(hwnd);
        await YieldToUiAsync();
        if (GetForegroundWindow() != hwnd)
            return marker + "INCONCLUSIVE Windows CI foreground input unavailable";

        TracksGrid.ScrollIntoView(_all[0]);
        await AwaitActualFrameAsync();
        TracksGrid.Focus(FocusState.Keyboard);
        await YieldToUiAsync();

        object? initialFocus = FocusManager.GetFocusedElement(TracksGrid.XamlRoot);
        if (!HasTableAncestor(initialFocus, TracksGrid))
            return marker + "INCONCLUSIVE table focus could not be established";

        int pressed = 0;
        int delivered = 0;
        KeyEventHandler observer = (sender, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Tab) delivered++;
        };
        TracksGrid.AddHandler(UIElement.KeyDownEvent, observer, true);
        try
        {
        for (int i = 0; i < 24; i++)
        {
            // Windows virtual key VK_TAB; key-up balances every key-down.
            // Dispatch through the actual foreground window input queue.
            keybd_event(0x09, 0, 0, UIntPtr.Zero);
            keybd_event(0x09, 0, 0x0002, UIntPtr.Zero);
            pressed++;
            await Task.Delay(75);
            await YieldToUiAsync();

            if (GetForegroundWindow() != hwnd)
                return marker + "INCONCLUSIVE foreground changed during injected input";
            var current = FocusManager.GetFocusedElement(TracksGrid.XamlRoot);
            if (!HasTableAncestor(current, TracksGrid))
                return marker + (delivered > 0 ? "PASS" : "INCONCLUSIVE") +
                    " Tab escaped table after " + pressed + " injected key press(es); " +
                    "Tab routed events received=" + delivered +
                    "; CI desktop observation, NOT physical keyboard/screenreader acceptance";
        }
        if (delivered == 0)
            return marker + "INCONCLUSIVE no routed Tab key events reached TableView";
        return marker + "BLOCKED Tab remained inside table after " + pressed +
            " actual foreground key press(es); Tab routed events received=" + delivered +
            "; do not approve WinUI production cutover";
        }
        finally
        {
            TracksGrid.RemoveHandler(UIElement.KeyDownEvent, observer);
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

    // A direct post-table focus transfer checks the separate reported issue:
    // successful Focus() that is secretly reverted by TableView on later frames.
    private async Task<string> ProbeExplicitFocusTransferAsync()
    {
        const string marker = "FOCUS_TRANSFER_OBSERVATION WinUI v1.5.1: ";
        if (_all.Count != 100_000) return marker + "INCONCLUSIVE wrong fixture";
        TracksGrid.ScrollIntoView(_all[0]);
        await AwaitActualFrameAsync();
        TracksGrid.Focus(FocusState.Keyboard);
        await YieldToUiAsync();
        if (!HasTableAncestor(FocusManager.GetFocusedElement(TracksGrid.XamlRoot), TracksGrid))
            return marker + "INCONCLUSIVE table focus not established";
        bool requested = FocusEscapeTarget.Focus(FocusState.Keyboard);
        await Task.Delay(180);
        await YieldToUiAsync();
        await AwaitActualFrameAsync();
        var focused = FocusManager.GetFocusedElement(TracksGrid.XamlRoot);
        return marker + (requested && ReferenceEquals(focused, FocusEscapeTarget)
            ? "PASS outside button retained keyboard focus after WinUI TableView"
            : "BLOCKED outside focus request was ignored or reverted to " +
                (focused?.GetType().Name ?? "<none>"));
    }

}
