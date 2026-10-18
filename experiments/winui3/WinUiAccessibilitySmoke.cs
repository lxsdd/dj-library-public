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
        return $"PASS WinUI accessibility: real TableView automation peer, " +
            $"stable name and declared control type {type} (not screen-reader acceptance)";
    }
}
