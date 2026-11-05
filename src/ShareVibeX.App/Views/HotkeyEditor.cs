using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;


using ShareVibeX.Core.Platform;

namespace ShareVibeX.App.Views;

internal sealed class HotkeyEditor : StackPanel
{
    private readonly Button change;

    private bool listening;
    public string Shortcut { get; private set; }

    public HotkeyEditor(string shortcut)
    {
        Shortcut = shortcut;
        change = Ui.Button(shortcut, ToggleListening);
        AutomationProperties.SetName(change, Loc.T("Change shortcut"));
        ToolTip.SetTip(change, Loc.T("Change shortcut"));

        Children.Add(change);
        change.LostFocus += (_, _) => Cancel();
        change.AddHandler(KeyDownEvent, CaptureKey, RoutingStrategies.Tunnel);
        change.AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.PhysicalKey == PhysicalKey.PrintScreen) CaptureKey(change, e);
        }, RoutingStrategies.Tunnel);
    }

    private void ToggleListening()
    {
        if (listening) { Cancel(); return; }
        change.Focus();
        listening = true;
        change.Content = Loc.T("Press a key or combination…");
    }

    public void Cancel()
    {
        listening = false;
        change.Content = Shortcut;
    }

    public bool TryCaptureRegisteredShortcut(string shortcut)
    {
        if (!listening) return false;
        Shortcut = HotkeyGesture.Parse(shortcut).ToString();
        Cancel();
        return true;
    }

    private void CaptureKey(object? sender, KeyEventArgs e)
    {
        if (!listening || e.Key == Key.Tab) return;
        e.Handled = true;
        if (e.Key == Key.Escape) { Cancel(); return; }
        uint modifiers = 0;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 1;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= 2;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 4;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= 8;

        var key = e.PhysicalKey;
        var code = key switch
        {
            PhysicalKey.PrintScreen => 44,
            >= PhysicalKey.F1 and <= PhysicalKey.F12 => 112 + (key - PhysicalKey.F1),
            >= PhysicalKey.A and <= PhysicalKey.Z => 'A' + (key - PhysicalKey.A),
            >= PhysicalKey.Digit0 and <= PhysicalKey.Digit9 => '0' + (key - PhysicalKey.Digit0),
            _ => 0
        };
        if (code == 0 || (modifiers == 0 && code is (>= '0' and <= '9') or (>= 'A' and <= 'Z'))) return;
        TryCaptureRegisteredShortcut(new HotkeyGesture(modifiers, (uint)code).ToString());
    }
}
