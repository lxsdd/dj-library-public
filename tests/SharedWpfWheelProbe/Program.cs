// A real native Win32 message into an independently hosted .NET 10 WPF window.
// Executes ONLY on ephemeral public GitHub Actions Windows runners, no user files.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Lxsdd.SharedDesktop.Wpf;

internal static class SharedWpfWheelProbe
{
    private const uint WmMouseHWheel = 0x020E;

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(
        IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static ScrollViewer FindScrollableViewer(DependencyObject node)
    {
        if (node == null) return null;
        ScrollViewer scroll = node as ScrollViewer;
        if (scroll != null && scroll.ScrollableWidth > 0.5) return scroll;
        int children = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < children; i++)
        {
            var found = FindScrollableViewer(VisualTreeHelper.GetChild(node, i));
            if (found != null) return found;
        }
        return null;
    }

    private static void SendHorizontal(IntPtr window, int wheelDelta)
    {
        // The signed high word is the documented WM_MOUSEHWHEEL wheel delta.
        int parameter = unchecked(wheelDelta << 16);
        SendMessage(window, WmMouseHWheel, new IntPtr(parameter), IntPtr.Zero);
    }

    private static void VerifyWindow(int ordinal)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            Width = 480,
            Height = 250,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible
        };
        for (int i = 0; i < 12; i++)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Synthetic column " + i,
                Width = new DataGridLength(180),
                Binding = new Binding(".")
            });
        }
        grid.ItemsSource = Enumerable.Range(0, 24)
            .Select(i => "Synthetic track " + i).ToList();

        var window = new Window
        {
            Title = "Disposable shared WPF wheel test",
            Width = 540,
            Height = 345,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = grid
        };
        try
        {
            HorizontalScrollSupport.Enable(window);
            HorizontalScrollSupport.Enable(grid);
            // Idempotence: calling Enable twice must never duplicate hooks.
            HorizontalScrollSupport.Enable(window);
            HorizontalScrollSupport.Enable(grid);
            window.Show();
            window.UpdateLayout();
            grid.UpdateLayout();
            Require(HorizontalScrollSupport.IsEnabled(grid),
                "real DataGrid did not register the pinned source router");
            Require(window.Activate(), "isolated WPF test window could not activate");
            Require(grid.Focus() && Keyboard.Focus(grid) != null,
                "native wheel target DataGrid could not receive keyboard focus");

            ScrollViewer viewer = FindScrollableViewer(grid);
            Require(viewer != null && viewer.ScrollableWidth > 200,
                "real WPF DataGrid did not create an overflowing horizontal ScrollViewer");
            viewer.ScrollToHorizontalOffset(0);
            viewer.ScrollToVerticalOffset(0);
            window.UpdateLayout();
            var handle = new WindowInteropHelper(window).Handle;
            Require(handle != IntPtr.Zero, "real WPF native HWND not available");

            double verticalBefore = viewer.VerticalOffset;
            double start = viewer.HorizontalOffset;
            SendHorizontal(handle, 120);
            window.UpdateLayout();
            double right = viewer.HorizontalOffset;
            Require(right > start + 1 && right <= start + 125,
                "WM_MOUSEHWHEEL +120 did not move exactly one horizontal step");

            SendHorizontal(handle, 0);
            window.UpdateLayout();
            Require(Math.Abs(viewer.HorizontalOffset - right) < 0.1,
                "zero horizontal wheel delta changed offset");

            SendHorizontal(handle, -120);
            window.UpdateLayout();
            double left = viewer.HorizontalOffset;
            Require(left >= 0 && left < right - 1 &&
                    Math.Abs(left - start) <= 1,
                "WM_MOUSEHWHEEL -120 did not restore horizontal position");
            Require(Math.Abs(viewer.VerticalOffset - verticalBefore) < 0.1,
                "horizontal input silently changed vertical scroll position");

            Console.WriteLine("PASS_SHARED_WPF_NET10_REAL_WM_MOUSEHWHEEL_RIGHT_LEFT_ZERO_VERTICAL " +
                ordinal + " right=" + right + " left=" + left);
        }
        finally
        {
            window.Close();
        }
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            if (!String.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                "true", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Refusing to run outside a disposable GitHub Actions Windows runner");

            var application = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            try
            {
                VerifyWindow(1);
                // Source HWND hooks and Window router dictionary must clean up
                // so a second real WPF window can attach and scroll again.
                VerifyWindow(2);
                Console.WriteLine("PASS_SHARED_WPF_NET10_WINDOW_CLOSE_AND_REATTACH");
                return 0;
            }
            finally
            {
                application.Shutdown();
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL shared WPF real HWND wheel: " + error);
            return 1;
        }
    }
}
