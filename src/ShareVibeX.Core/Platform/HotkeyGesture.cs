namespace ShareVibeX.Core.Platform;

public readonly record struct HotkeyGesture(uint Modifiers, uint VirtualKey)
{
    public static HotkeyGesture Parse(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries);

        uint modifiers = 0;
        foreach (var part in parts[..^1])
        {
            uint flag = part.ToUpperInvariant() switch { "ALT" => 1, "CTRL" => 2, "SHIFT" => 4, "WIN" => 8, _ => throw Invalid() };
            if ((modifiers & flag) != 0) throw Invalid();
            modifiers |= flag;
        }
        var key = parts[^1].ToUpperInvariant();
        uint code;
        if (key == "PRINTSCREEN") code = 44;
        else if (key.StartsWith('F') && int.TryParse(key.AsSpan(1), out var number) && number is >= 1 and <= 12) code = (uint)(111 + number);
        else if (key.Length == 1 && key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9') code = key[0];
        else throw Invalid();
        if (modifiers == 0 && code != 44 && code is not (>= 112 and <= 123)) throw Invalid();
        return new(modifiers, code);
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & 2) != 0) parts.Add("Ctrl");
        if ((Modifiers & 1) != 0) parts.Add("Alt");
        if ((Modifiers & 4) != 0) parts.Add("Shift");
        if ((Modifiers & 8) != 0) parts.Add("Win");
        parts.Add(VirtualKey switch
        {
            44 => "PrintScreen",
            >= 112 and <= 123 => "F" + (VirtualKey - 111),
            _ => ((char)VirtualKey).ToString()
        });
        return string.Join("+", parts);
    }

    private static FormatException Invalid() => new(Loc.T("Use PrintScreen or F1–F12, or combine A–Z or 0–9 with Ctrl, Alt, Shift or Win."));
}
