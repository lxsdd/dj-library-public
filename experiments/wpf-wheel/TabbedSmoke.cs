// Real WPF TabControl regression using BOTH unchanged production adapters.
// Only synthetic rows and sent HWND messages. No app settings or user files.
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace HorizontalWheelSmoke
{
    internal static class TabbedSmoke
    {
        private const int WmMouseHWheel = 0x020E;
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        public static void RunOne(string label, Action<DataGrid> enable)
        {
            var window = new Window
            {
                Title = "Two-tab native wheel contract: " + label,
                Width = 760, Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            var tabs = new TabControl();
            var trackRows = MakeRows("track");
            var discRows = MakeRows("disc");
            var tracks = CreateGrid(trackRows);
            var discs = CreateGrid(discRows);
            tabs.Items.Add(new TabItem { Header = "Tracks", Content = tracks });
            tabs.Items.Add(new TabItem { Header = "CDs", Content = discs });
            window.Content = tabs;

            // DJ Library hooks its Window, MyTagSync hooks Window + grids.
            // This adapter-specific test must exercise the grid-loaded path
            // as well as idempotence for each original implementation.
            enable(tracks);
            enable(discs);
            enable(tracks);
            window.Show();
            window.Activate();
            Pump(window);
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException(label + ": missing tabbed WPF HWND");
            try
            {
                var trackScroll = FocusAndFind(window, tracks);
                if (trackScroll.ScrollableWidth < 100)
                    throw new InvalidOperationException(label + ": Tracks missing horizontal extent");
                tracks.SelectedItem = trackRows[0];
                trackScroll.ScrollToHorizontalOffset(0);
                Pump(window);
                Inject(hwnd, 120);
                Pump(window);
                AssertOneStep(label + " Tracks", trackScroll.HorizontalOffset);

                tabs.SelectedIndex = 1;
                Pump(window);
                var discScroll = FocusAndFind(window, discs);
                if (discScroll.ScrollableWidth < 100)
                    throw new InvalidOperationException(label + ": CDs missing horizontal extent");
                discs.SelectedItem = discRows[1];
                discScroll.ScrollToHorizontalOffset(0);
                Pump(window);
                Inject(hwnd, 120);
                Pump(window);
                AssertOneStep(label + " CDs", discScroll.HorizontalOffset);
                if (Math.Abs(trackScroll.HorizontalOffset - 72) > 6)
                    throw new InvalidOperationException(label + ": switching to CDs leaked wheel scroll into hidden Tracks tab");

                tabs.SelectedIndex = 0;
                Pump(window);
                FocusAndFind(window, tracks);
                if (!ReferenceEquals(tracks.SelectedItem, trackRows[0]) ||
                    !ReferenceEquals(discs.SelectedItem, discRows[1]))
                    throw new InvalidOperationException(label + ": tab switching lost selected row identities");
                trackScroll.ScrollToHorizontalOffset(0);
                Pump(window);
                if (trackScroll.HorizontalOffset > 1)
                    throw new InvalidOperationException(label + ": could not return to first column after tab switch");
                // Repeated registration after the visible Window is loaded
                // must not attach an additional HwndSourceHook.
                enable(tracks);
                Inject(hwnd, 120);
                Pump(window);
                AssertOneStep(label + " repeated Enable", trackScroll.HorizontalOffset);
                if (Math.Abs(discScroll.HorizontalOffset - 72) > 6)
                    throw new InvalidOperationException(label + ": switching back leaked wheel into hidden CDs tab");
                Console.WriteLine("PASS " + label +
                    " two-tab WPF HWND: Tracks/CDs independent +120 notch, idempotent hook, row selection, first-column return");
            }
            finally
            {
                window.Close();
                Pump(window);
            }
        }

        private static Row[] MakeRows(string prefix) =>
            Enumerable.Range(0, 80).Select(i => new Row { Title = prefix + "-" + i }).ToArray();

        private static DataGrid CreateGrid(Row[] rows)
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                EnableRowVirtualization = true,
                ItemsSource = rows
            };
            for (int i = 0; i < 24; i++)
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Column " + i,
                    Binding = new System.Windows.Data.Binding("Title"),
                    Width = new DataGridLength(190)
                });
            return grid;
        }

        private static ScrollViewer FocusAndFind(Window window, DataGrid grid)
        {
            if (!grid.IsVisible)
                throw new InvalidOperationException("Expected a visible selected tab DataGrid");
            grid.Focus();
            Keyboard.Focus(grid);
            Pump(window);
            var viewer = FindViewer(grid);
            if (viewer == null) throw new InvalidOperationException("Visible tab DataGrid has no horizontal ScrollViewer");
            return viewer;
        }

        private static ScrollViewer? FindViewer(DependencyObject node)
        {
            if (node is ScrollViewer viewer && viewer.ScrollableWidth > 0.5) return viewer;
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var found = FindViewer(VisualTreeHelper.GetChild(node, i));
                if (found != null) return found;
            }
            return null;
        }

        private static void Pump(Window window)
        {
            window.Dispatcher.Invoke((Action)(() => { }), DispatcherPriority.ApplicationIdle);
            if (window.IsVisible) window.UpdateLayout();
        }

        private static void Inject(IntPtr hwnd, short delta)
        {
            var wparam = new IntPtr(unchecked((int)((uint)(ushort)delta << 16)));
            SendMessage(hwnd, WmMouseHWheel, wparam, IntPtr.Zero);
        }

        private static void AssertOneStep(string label, double offset)
        {
            if (offset < 65 || offset > 79)
                throw new InvalidOperationException(label + ": expected one 72px notch, got " + offset +
                    " (missing routing, duplicate hooks, or wrong active tab)");
        }

        private sealed class Row
        {
            public string Title { get; set; } = "";
        }
    }
}
