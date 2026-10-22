using System.Windows;
using System.Windows.Controls;

namespace DJLibrary
{
    // Stable application-facing API. All routing behavior is implemented in the
    // exact SHA-pinned shared source in SharedHorizontalScrollSupport.cs.
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
