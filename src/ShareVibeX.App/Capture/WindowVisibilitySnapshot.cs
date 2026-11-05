using Avalonia.Controls;

namespace ShareVibeX.App.Capture;

public sealed class WindowVisibilitySnapshot
{
    private readonly List<Entry> hidden = [];
    public void Hide(IEnumerable<Window> windows)
    {
        foreach(var entry in hidden)entry.Detach();
        hidden.Clear();
        hidden.AddRange(windows.Where(window => window.IsVisible)
            .OrderBy(Depth).Select(window=>new Entry(window,window.Owner as Window)));
        foreach (var entry in hidden.AsEnumerable().Reverse()) entry.Window.Hide();
    }
    public void Restore()
    {
        var entries=hidden.ToArray();
        hidden.Clear();
        var errors=new List<Exception>();
        try
        {
            foreach(var entry in entries)
            {
                if(entry.IsClosed || entries.Any(owner=>owner.Window==entry.Owner && owner.IsClosed))continue;
                try{if(entry.Owner is null)entry.Window.Show();else entry.Window.Show(entry.Owner);}
                catch(Exception error){errors.Add(error);}
            }
        }
        finally{foreach(var entry in entries)entry.Detach();}
        if(errors.Count>0)throw new AggregateException(Loc.T("Some windows could not be restored after capture."),errors);
    }

    private static int Depth(Window window)
    {
        var depth = 0;
        while (window.Owner is Window owner) { depth++; window = owner; }
        return depth;
    }

    private sealed class Entry
    {
        public Window Window {get;}
        public Window? Owner {get;}
        public bool IsClosed {get;private set;}
        public Entry(Window window,Window? owner){Window=window;Owner=owner;Window.Closed+=OnClosed;}
        private void OnClosed(object? sender,EventArgs args)=>IsClosed=true;
        public void Detach()=>Window.Closed-=OnClosed;
    }
}
