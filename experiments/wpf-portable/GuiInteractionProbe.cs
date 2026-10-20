// Real external-process UI Automation smoke of the unchanged DJ Library MainWindow.
// Never writes settings, never imports a real catalog. Starts only against disposable CI PID.
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

internal static class DJLibraryRealUiProbe
{
    private const byte KeyUp = 0x02;
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool MoveWindow(
        IntPtr hwnd, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    private static void Require(bool success, string context)
    {
        if (!success) throw new InvalidOperationException("Real WPF UX: " + context);
    }
    private static AutomationElement Root(IntPtr hwnd)
    {
        var x = AutomationElement.FromHandle(hwnd);
        Require(x != null, "automation root missing");
        return x;
    }
    private static AutomationElement[] Tabs(IntPtr hwnd)
    {
        var root = Root(hwnd);
        var criteria = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
        var hits = root.FindAll(TreeScope.Descendants, criteria);
        return hits.Cast<AutomationElement>().Where(x =>
            x.Current.Name == "Tracks" || x.Current.Name == "CDs").ToArray();
    }
    private static AutomationElement Tab(IntPtr hwnd, string name)
    {
        var hits = Tabs(hwnd).Where(x => x.Current.Name == name).ToArray();
        Require(hits.Length == 1, "one real tab named " + name + ", observed " + hits.Length);
        return hits[0];
    }
    private static bool IsSelected(AutomationElement element)
    {
        return ((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern))
            .Current.IsSelected;
    }
    private static void Select(AutomationElement element)
    {
        ((SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Thread.Sleep(175);
        Require(IsSelected(element), "UIA selected item rejected");
    }
    private static void Key(byte key) 
    {
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KeyUp, UIntPtr.Zero);
    }
    private static void Ctrl(byte key)
    {
        keybd_event(0x11, 0, 0, UIntPtr.Zero);
        try { Key(key); }
        finally { keybd_event(0x11, 0, KeyUp, UIntPtr.Zero); }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            int pid = 0;
            bool reopened = args.Length == 2 && args[1] == "--restored";
            Require((args.Length == 1 || reopened) && Int32.TryParse(args[0], out pid) && pid > 0,
                "requires disposable hosted GUI process PID");
            using var process = Process.GetProcessById(pid);
            IntPtr hwnd = process.MainWindowHandle;
            Require(hwnd != IntPtr.Zero, "visible MainWindow HWND absent");
            Require(process.MainWindowTitle.StartsWith("DJ Library", StringComparison.Ordinal),
                "unexpected WPF product/window");
            var tracks = Tab(hwnd, "Tracks");
            var cds = Tab(hwnd, "CDs");
            Require(Tabs(hwnd).Length == 2, "two distinct real tab elements expected");
            Require(tracks.Current.BoundingRectangle.Width > 20 &&
                cds.Current.BoundingRectangle.Width > 20, "real tab headers invisible");
            if (reopened)
            {
                // This is the second launch: it must restore the CDs tab
                // persisted by the previous instance's real Closing hook.
                Thread.Sleep(250);
                Require(IsSelected(Tab(hwnd, "CDs")),
                    "real SettingsManager XML did not restore selected CDs tab");
                ((WindowPattern)Root(hwnd).GetCurrentPattern(WindowPattern.Pattern)).Close();
                Console.WriteLine("PASS restored real WPF CDs tab on second startup and closed normally");
                return 0;
            }
            Require(IsSelected(tracks), "fresh empty DB must select Tracks by default");
            Select(cds);
            Require(!IsSelected(tracks), "Tracks remained selected after CDs UIA selection");
            Select(tracks);
            Require(!IsSelected(cds), "CDs remained selected after Tracks UIA selection");
            Console.WriteLine("PASS real WPF UIA tab headers: Tracks/CDs selection both directions");

            Require(SetForegroundWindow(hwnd), "cannot give disposable WPF process foreground");
            Thread.Sleep(180);
            Require(GetForegroundWindow() == hwnd, "foreground focus not held by WPF window");
            Ctrl(0x32); // Ctrl+2 -> CDs
            Thread.Sleep(260);
            Require(IsSelected(Tab(hwnd, "CDs")), "Ctrl+2 did not select CDs");
            Ctrl(0x31); // Ctrl+1 -> Tracks
            Thread.Sleep(260);
            Require(IsSelected(Tab(hwnd, "Tracks")), "Ctrl+1 did not select Tracks");
            Console.WriteLine("PASS actual routed keyboard Ctrl+1/Ctrl+2 switching Tracks/CDs");

            Ctrl(0x46); // Ctrl+F -> Search edit
            Thread.Sleep(200);
            AutomationElement focused = AutomationElement.FocusedElement;
            Require(focused != null && focused.Current.ProcessId == pid &&
                focused.Current.ControlType == ControlType.Edit,
                "Ctrl+F did not focus the real Search edit");
            var value = focused.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            Require(value != null && !value.Current.IsReadOnly,
                "Search edit has no editable UIA ValuePattern");
            value.SetValue("SYNTHETIC_NO_MATCH_ONLY");
            Thread.Sleep(240);
            Require(value.Current.Value == "SYNTHETIC_NO_MATCH_ONLY",
                "Search edit rejected synthetic text");
            Key(0x1B); // Escape clears search
            Thread.Sleep(250);
            Require(value.Current.Value == "", "Escape did not clear Search edit");
            Console.WriteLine("PASS real WPF keyboard Ctrl+F, synthetic search edit and Escape clear");

            Require(GetWindowRect(hwnd, out Rect original), "cannot observe WPF window bounds");
            int wantedWidth = original.Width - 46;
            Require(wantedWidth >= 980, "runner window too narrow for valid resize");
            Require(MoveWindow(hwnd, original.Left, original.Top, wantedWidth,
                original.Height, true), "MoveWindow resize failed");
            Thread.Sleep(300);
            Require(GetWindowRect(hwnd, out Rect resized) &&
                Math.Abs(resized.Width - wantedWidth) <= 14 &&
                Math.Abs(resized.Height - original.Height) <= 14,
                "WPF window failed strict native resize");
            Require(Tabs(hwnd).Length == 2, "WPF tabs disappeared after resize");
            Require(MoveWindow(hwnd, original.Left, original.Top, original.Width,
                original.Height, true), "MoveWindow restore failed");
            Thread.Sleep(250);
            Require(GetWindowRect(hwnd, out Rect back) &&
                Math.Abs(back.Width - original.Width) <= 14,
                "WPF window size not restored");
            Console.WriteLine("PASS actual WPF window native resize and restore without lost tabs");
            Select(Tab(hwnd, "CDs"));
            ((WindowPattern)Root(hwnd).GetCurrentPattern(WindowPattern.Pattern)).Close();
            Console.WriteLine("PASS requested normal WPF close with CDs tab selected for persistence");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL real WPF UI interaction: " + ex);
            return 1;
        }
    }
}
