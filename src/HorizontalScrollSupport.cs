using System.Windows;
using System.Windows.Controls;

namespace DJLibrary
{
    // Preserve the original public DJ Library API while sharing an immutable
    // source-pinned WPF mousewheel router with rekordbox MyTagSync.
    public static class HorizontalScrollSupport
    {
        public static void Enable(DataGrid grid)
        {
            Lxsdd.SharedDesktop.Wpf.HorizontalScrollSupport.Enable(grid);
        }

        public static bool IsEnabled(DataGrid grid)
        {
            return Lxsdd.SharedDesktop.Wpf.HorizontalScrollSupport.IsEnabled(grid);
        }

        public static void Enable(Window window)
        {
            Lxsdd.SharedDesktop.Wpf.HorizontalScrollSupport.Enable(window);
        }
    }
}
