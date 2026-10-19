using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DJLibrary.WinUiSpike;

// Experimental adapter: upstream TableView v1.5.1 consumes Tab and wraps
// first/last rows. Preserve its handling everywhere except true boundaries.
public sealed class KeyboardBoundaryTableView : WinUI.TableView.TableView
{
    [DllImport("user32.dll")] private static extern short GetKeyState(int vk);
    public Control? FocusBeforeTable { get; set; }
    public Control? FocusAfterTable { get; set; }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        if (IsReadOnly && e.Key == VirtualKey.Tab && Items.Count > 0)
        {
            bool reverse = (GetKeyState(0x10) & unchecked((short)0x8000)) != 0;
            var cell = CurrentCellSlot;
            int count = Columns.VisibleColumns.Count;
            bool atStart = cell.HasValue && cell.Value.Row == 0 && cell.Value.Column == 0;
            bool atEnd = cell.HasValue && cell.Value.Row == Items.Count - 1 &&
                         cell.Value.Column == count - 1;
            if ((reverse && atStart) || (!reverse && atEnd))
            {
                Control? target = reverse ? FocusBeforeTable : FocusAfterTable;
                if (target?.Focus(FocusState.Keyboard) == true)
                {
                    e.Handled = true;
                    return;
                }
            }
        }
        base.OnKeyDown(e);
    }
}
