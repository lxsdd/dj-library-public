using System;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;

namespace DJLibrary.WinUiSpike;

// A real WinUI TableView must expose a peer and a stable accessible name;
// this is NOT a substitute for NVDA/Narrator/keyboard/screen-reader acceptance.
public sealed partial class MainWindow
{
    private string VerifyTableAccessibilityContract()
    {
        const string expectedName = "DJ Library synthetic track table";
        string attachedName = AutomationProperties.GetName(TracksGrid);
        if (!string.Equals(attachedName, expectedName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "WinUI TableView has no stable AutomationProperties.Name");

        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(TracksGrid);
        if (peer == null)
            throw new InvalidOperationException("WinUI TableView has no UI Automation peer");
        string actualName = peer.GetName();
        if (!string.Equals(actualName, expectedName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "WinUI UI Automation peer name mismatch: " + actualName);

        var type = peer.GetAutomationControlType();
        var grid = peer.GetPattern(PatternInterface.Grid) as
            Microsoft.UI.Xaml.Automation.Provider.IGridProvider;
        if (grid == null)
            throw new InvalidOperationException(
                "WinUI TableView has no actual UI Automation Grid pattern provider");
        if (grid.RowCount != TracksGrid.Items.Count || grid.ColumnCount != TracksGrid.Columns.Count)
            throw new InvalidOperationException(
                $"WinUI UIA Grid shape differs from runtime: " +
                $"provider={grid.RowCount}x{grid.ColumnCount}, " +
                $"table={TracksGrid.Items.Count}x{TracksGrid.Columns.Count}");
        var selection = peer.GetPattern(PatternInterface.Selection) as
            Microsoft.UI.Xaml.Automation.Provider.ISelectionProvider;
        if (selection == null)
            throw new InvalidOperationException(
                "WinUI TableView has no actual UI Automation Selection pattern provider");

        // A merely named DataGrid is insufficient for navigating a large
        // music catalog with assistive technology. Table headers must also
        // be accessible via the real UIA Table pattern.
        var table = peer.GetPattern(PatternInterface.Table) as
            Microsoft.UI.Xaml.Automation.Provider.ITableProvider;
        if (table == null)
            throw new InvalidOperationException(
                "WinUI TableView has no UI Automation Table provider");
        var columns = table.GetColumnHeaders();
        if (columns == null || columns.Length != grid.ColumnCount)
            throw new InvalidOperationException(
                $"WinUI UIA Table header count mismatch: " +
                $"headers={columns?.Length ?? -1}, expected={grid.ColumnCount}");

        return $"PASS WinUI accessibility: real TableView automation peer, " +
            $"stable name, control type {type}, UIA Grid {grid.RowCount}x{grid.ColumnCount}, " +
            $"Selection and Table patterns, {columns.Length} navigable column headers " +
            "(not physical screen-reader/keyboard acceptance)";
    }
}
