using System.Windows.Controls;

namespace DJLibrary
{
    // These are inert metadata configuration dependencies referenced by AppSettings.
    // All behavior under test is compiled verbatim from production Settings,
    // WindowGeometrySettings and GridLayoutSettings.
    public sealed class FilterState { }
    public sealed class CdMetadataFetchOptions { public bool UseDiscogs = true; }
    public static class UiHelpers
    {
        public static DataGrid CreateReadOnlyGrid()
        {
            return new DataGrid
            {
                IsReadOnly = true,
                AutoGenerateColumns = false,
                CanUserSortColumns = true,
                CanUserReorderColumns = true,
                CanUserResizeColumns = true
            };
        }
    }
}
