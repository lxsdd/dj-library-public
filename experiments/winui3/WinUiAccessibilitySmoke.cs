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

        return $"PASS WinUI accessibility: real TableView automation peer, " +
            $"stable name, control type {type}, UIA Grid {grid.RowCount}x{grid.ColumnCount}, " +
            "Selection pattern (not a physical screen-reader or keyboard acceptance test)";
    }
}
