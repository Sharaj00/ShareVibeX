using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ShareVibeX.App.Media;
using ShareVibeX.App.Views;

namespace ShareVibeX.App.History;

public sealed class CaptureHistoryPage:StackPanel,IDisposable
{
    private readonly StackPanel rows=new(){Spacing=8};private readonly TextBlock status=Ui.Text("",12,true);
    private readonly TextBlock errorText=Ui.Text("",12,true);
    private readonly List<Bitmap> bitmaps=[];private readonly CaptureHistory history;private readonly Func<string?> ffmpegPath;
    private readonly Action<string> open;
    private readonly Action<string,nint,int,int> menu;
    private readonly Func<string,nint,Task<bool>> recycle;
    private CancellationTokenSource? loading;private int generation;private bool closed,active,busy;
    private Task? refreshing;
    private readonly HashSet<Task> previews=[];
    public CaptureHistoryPage(CaptureHistory history,Func<string?>? ffmpegPath=null,
        Action<string>? open=null,Action<string,nint,int,int>? menu=null,Func<string,nint,Task<bool>>? recycle=null)
    {
        this.history=history;this.ffmpegPath=ffmpegPath??(()=>null);
        this.open=open??(path=>Process.Start(new ProcessStartInfo(path){UseShellExecute=true}));
        this.menu=menu??((path,owner,x,y)=>{if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException(Loc.T("Windows file actions are unavailable on this platform."));ShareVibeX.Platform.Windows.WindowsHistoryFileActions.ShowContextMenu(path,owner,x,y);});
        this.recycle=recycle??((path,owner)=>{if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException(Loc.T("Windows file actions are unavailable on this platform."));return ShareVibeX.Platform.Windows.WindowsHistoryFileActions.RecycleAsync(path,owner);});
        Spacing=8;
        Children.Add(status);Children.Add(errorText);Children.Add(rows);
    }
    public Task ActivateAsync(){if(closed)return Task.CompletedTask;active=true;if(busy)return Task.CompletedTask;return refreshing is {IsCompleted:false}?refreshing:refreshing=ReloadAsync();}
    public void Deactivate(){active=false;++generation;refreshing=null;CancelLoading();}
    public void Dispose(){if(closed)return;closed=true;Deactivate();}
    private void CancelLoading()
    {loading?.Cancel();loading?.Dispose();loading=null;rows.Children.Clear();foreach(var bitmap in bitmaps)bitmap.Dispose();bitmaps.Clear();}
    private async Task ReloadAsync()
    {
        CancelLoading();if(closed||!active)return;var revision=++generation;loading=new();var token=loading.Token;status.Text=Loc.T("Loading capture history…");
        try
        {
            var entries=await history.ListEntriesAsync(token);if(closed||!active||revision!=generation)return;
            foreach(var month in entries.GroupBy(e=>e.Month).OrderByDescending(g=>g.Key,StringComparer.Ordinal))
            {
                var children=new StackPanel{Spacing=8};var items=month.ToArray();var offset=0;
                var more=Ui.Button("Load more",()=>{});more.Click+=(_,_)=>LoadBatch();
                void LoadBatch()
                {
                    children.Children.Remove(more);
                    foreach(var item in items.Skip(offset).Take(30))children.Children.Add(CreateRow(item,revision,token));
                    offset=Math.Min(items.Length,offset+30);if(offset<items.Length)children.Children.Add(more);
                }
                var branch=new Expander{Header=$"{month.Key}  ({items.Length})",Content=children,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};
                branch.Expanding+=(_,_)=>{if(offset==0)LoadBatch();};rows.Children.Add(branch);
                if(rows.Children.Count==1){branch.IsExpanded=true;if(offset==0)LoadBatch();}
            }
            status.Text=entries.Count==0?Loc.T("No captures in history."):Loc.F("{0} captures",entries.Count);
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(Exception error){if(!closed&&revision==generation)status.Text=error.Message;}
    }
    private Control CreateRow(CaptureHistoryEntry item,int revision,CancellationToken token)
    {
        var image=new Image{Width=160,Height=100,Stretch=Stretch.Uniform};
        var placeholder=Ui.Text("Loading preview…",12,true);placeholder.HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center;placeholder.VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center;
        var preview=new Grid{Width=160,Height=100,Children={image,placeholder}};
        var name=Ui.Text(Path.GetFileName(item.Path),14);name.TextWrapping=TextWrapping.Wrap;
        var details=Ui.Stack(name,Ui.Text(item.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss"),12,true));Grid.SetColumn(details,1);
        var file=Ui.Button("",()=>Open(item.Path));file.Content=new Grid{ColumnDefinitions=new("160,*"),ColumnSpacing=16,Children={preview,details}};
        file.HorizontalContentAlignment=Avalonia.Layout.HorizontalAlignment.Stretch;file.Padding=new Thickness(8);
        file.Background=Brushes.Transparent;file.BorderThickness=new Thickness(0);
        AutomationProperties.SetName(file,"Open capture: "+Path.GetFileName(item.Path));ToolTip.SetTip(file,item.Path);
        file.ContextRequested+=async(_,args)=>{args.Handled=true;var point=args.TryGetPosition(file,out var position)?position:new Point(file.Bounds.Width/2,file.Bounds.Height/2);await ShowFileMenuAsync(item.Path,file.PointToScreen(point));};
        var remove=Ui.Button("",async()=>await RecycleFileAsync(item.Path));remove.Width=36;remove.Height=36;remove.Padding=new Thickness(8);
        remove.Background=new SolidColorBrush(Color.Parse("#C42B1C"));remove.Foreground=Brushes.White;
        remove.Content=new PathIcon{Width=18,Height=18,Data=Geometry.Parse("M4,6 L6,4 L12,10 L18,4 L20,6 L14,12 L20,18 L18,20 L12,14 L6,20 L4,18 L10,12 Z")};
        AutomationProperties.SetName(remove,"Delete capture: "+Path.GetFileName(item.Path));ToolTip.SetTip(remove,Loc.T("Move to Recycle Bin"));
        remove.VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center;Grid.SetColumn(remove,1);
        var card=new Grid{ColumnDefinitions=new("*,Auto"),ColumnSpacing=8,Children={file,remove}};
        var pending=LoadPreviewAsync();previews.Add(pending);_=ForgetPreviewAsync(pending);return card;
        async Task LoadPreviewAsync()
        {
            Bitmap? thumbnail=null;var transferred=false;
            try
            {
                thumbnail=await MediaThumbnailLoader.LoadAsync(item.Path,ffmpegPath(),240,token);
                await Dispatcher.UIThread.InvokeAsync(()=>
                {
                    if(closed||revision!=generation||token.IsCancellationRequested){thumbnail?.Dispose();transferred=true;return;}
                    if(thumbnail is null){placeholder.Text=Loc.T("Preview unavailable");return;}
                    transferred=true;bitmaps.Add(thumbnail);image.Source=thumbnail;placeholder.IsVisible=false;
                });
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested){}
            catch{if(!token.IsCancellationRequested&&!closed)placeholder.Text=Loc.T("Preview unavailable");}
            finally{if(!transferred)thumbnail?.Dispose();}
        }
    }
    private async Task ForgetPreviewAsync(Task task){try{await task;}finally{previews.Remove(task);}}
    private async Task ReleasePreviewReadersAsync(){loading?.Cancel();await Task.WhenAll(previews.ToArray());}
    private nint Owner=>TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle??0;
    private void Open(string path){if(busy)return;try{errorText.Text="";open(path);}catch(Exception error){errorText.Text=Loc.T(error.Message);}}
    public async Task ShowFileMenuAsync(string path,PixelPoint position)
    {
        if(busy||closed||!active)return;busy=true;errorText.Text="";
        try{await ReleasePreviewReadersAsync();if(!closed&&active)menu(path,Owner,position.X,position.Y);}
        catch(Exception error){errorText.Text=Loc.T(error.Message);}
        finally{busy=false;await (refreshing=ReloadAsync());}
    }
    public async Task RecycleFileAsync(string path)
    {
        if(busy||closed||!active)return;busy=true;IsEnabled=false;errorText.Text="";
        try{await ReleasePreviewReadersAsync();if(!closed&&active)await recycle(path,Owner);}
        catch(FileNotFoundException)when(!File.Exists(path)){}
        catch(Exception error){errorText.Text=Loc.T(error.Message);}
        finally{busy=false;IsEnabled=true;await (refreshing=ReloadAsync());}
    }
}
