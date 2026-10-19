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
                return marker + "PASS Tab escaped real table after " + pressed +
                    " injected key press(es); CI desktop observation, NOT human/screenreader acceptance";
        }
        return marker + "BLOCKED Tab remained inside table after " + pressed +
            " actual foreground key press(es); do not approve WinUI production cutover";
    }
}
