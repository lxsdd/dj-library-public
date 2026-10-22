using System;
using System.Collections.Generic;
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
    internal static class Smoke
    {
        private const int WmMouseHWheel = 0x020E;
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [STAThread]
        private static int Main()
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                RunOne("DJ Library", grid => DJLibrary.HorizontalScrollSupport.Enable(grid));
                RunOne("rekordbox MyTagSync", grid => RekordboxMyTagSync.HorizontalScrollSupport.Enable(grid));
                // Match actual production MainWindow hookups exactly:
                // DJ Library -> Enable(this), MyTagSync -> Enable(this) + Enable(each DataGrid).
                TabbedSmoke.RunOne("DJ Library",
                    window => DJLibrary.HorizontalScrollSupport.Enable(window), null);
                TabbedSmoke.RunOne("rekordbox MyTagSync",
                    window => RekordboxMyTagSync.HorizontalScrollSupport.Enable(window),
                    grid => RekordboxMyTagSync.HorizontalScrollSupport.Enable(grid));
                app.Shutdown();
                Console.WriteLine("PASS: both actual horizontal-wheel routers handle single-grid and two-tab WPF windows, repeated enable, selection and scroll isolation");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("FAIL: " + e);
                return 1;
            }
        }

        private static void RunOne(string label, Action<DataGrid> enable)
        {
            var window = new Window
            {
                Title = "Horizontal wheel contract: " + label,
                Width = 760,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                EnableRowVirtualization = true
            };
            for (int col = 0; col < 24; col++)
            {
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Column " + col,
                    Binding = new System.Windows.Data.Binding("Title"),
                    Width = new DataGridLength(190)
                });
            }
            grid.ItemsSource = Enumerable.Range(1, 60).Select(i => new Track { Title = "Synthetic " + i }).ToArray();
            window.Content = grid;
            enable(grid);
            window.Show();
            window.Activate();
            Pump(window);
            grid.Focus();
            Keyboard.Focus(grid);
            Pump(window);

            try
            {
                var viewer = FindScrollableViewer(grid);
                if (viewer == null || viewer.ScrollableWidth < 100)
                    throw new InvalidOperationException(label + ": grid did not expose horizontally scrollable WPF ScrollViewer");

                var handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero) throw new InvalidOperationException(label + ": missing real window HWND");

                viewer.ScrollToHorizontalOffset(0);
                Pump(window);
                SendNativeWheel(handle, 120);
                Pump(window);
                var forward = viewer.HorizontalOffset;
                if (forward < 1 || forward > 80)
                    throw new InvalidOperationException(label + ": +120 native horizontal wheel did not scroll right by one notch; offset=" + forward);

                SendNativeWheel(handle, -120);
                Pump(window);
                if (viewer.HorizontalOffset > 1)
                    throw new InvalidOperationException(label + ": -120 native horizontal wheel did not scroll left; offset=" + viewer.HorizontalOffset);

                viewer.ScrollToHorizontalOffset(viewer.ScrollableWidth - 12);
                Pump(window);
                SendNativeWheel(handle, 120);
                Pump(window);
                if (viewer.HorizontalOffset > viewer.ScrollableWidth + 0.5
                    || viewer.HorizontalOffset < viewer.ScrollableWidth - 1)
                    throw new InvalidOperationException(label + ": native horizontal wheel did not clamp at right edge");

                SendNativeWheel(handle, 0);
                Pump(window);
                if (Math.Abs(viewer.HorizontalOffset - viewer.ScrollableWidth) > 1)
                    throw new InvalidOperationException(label + ": zero-delta wheel must not move");

                Console.WriteLine("PASS " + label + " native +120/-120/zero and right-edge clamp; scrollableWidth=" + viewer.ScrollableWidth);
            }
            finally
            {
                // Actual Window.Closed causes each product router to release its HwndSource hook.
                window.Close();
                Pump(window);
            }
        }

        private static void SendNativeWheel(IntPtr handle, short delta)
        {
            var wparam = new IntPtr(unchecked((int)((uint)(ushort)delta << 16)));
            SendMessage(handle, WmMouseHWheel, wparam, IntPtr.Zero);
        }

        private static void Pump(Window window)
        {
            window.Dispatcher.Invoke((Action)(() => { }), DispatcherPriority.ApplicationIdle);
            if (window.IsVisible) window.UpdateLayout();
        }

        private static ScrollViewer? FindScrollableViewer(DependencyObject root)
        {
            var viewer = root as ScrollViewer;
            if (viewer != null && viewer.ScrollableWidth > 0.5) return viewer;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var result = FindScrollableViewer(VisualTreeHelper.GetChild(root, i));
                if (result != null) return result;
            }
            return null;
        }

        private sealed class Track
        {
            public string Title { get; set; } = string.Empty;
        }
    }
}
