using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ShareVibeX.App.Views;

internal static class Ui
{
    public static Button Button(string text, Action action, bool accent = false)
    {
        var button = new Button { Content = Loc.T(text), HorizontalAlignment = HorizontalAlignment.Stretch };
        if (accent) button.Classes.Add("accent");
        button.Click += (_, _) => action();
        return button;
    }

    public static TextBlock Text(string text, double size = 14, bool muted = false)
    {
        var block = new TextBlock { Text = Loc.T(text), FontSize = size, TextWrapping = TextWrapping.Wrap };
        if (muted) block.Classes.Add("muted");
        return block;
    }

    public static StackPanel Stack(params Control[] children)
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    public static Border Card(Control content)
    {
        var border = new Border { Child = content };
        border.Classes.Add("card");
        return border;
    }

    public static void WindowStyle(Window window)
    {
        window.Bind(Window.BackgroundProperty, window.GetResourceObservable("WindowBackground"));
    }
}
