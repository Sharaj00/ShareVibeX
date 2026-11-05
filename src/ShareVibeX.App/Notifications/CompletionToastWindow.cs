using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ShareVibeX.App.Views;
using ShareVibeX.Core.Settings;
using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Notifications;

internal sealed class CompletionToastWindow:Window,IDisposable
{
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(33)};
    private readonly Stopwatch elapsed=new();
    private readonly CancellationTokenSource previewCancellation=new();
    private readonly Image preview=new(){Stretch=Stretch.Uniform};
    private Avalonia.Media.Imaging.Bitmap? previewBitmap;
    private CompletionToastWindow(CompletionNotice notice,CompletionOptions options)
    {
        Title="ShareVibeX";WindowDecorations=WindowDecorations.None;CanResize=false;ShowActivated=false;
        ShowInTaskbar=false;Topmost=true;CapturePanelStyle.Apply(this);Opacity=0;
        var filename=new TextBlock{Text=notice.Path is null?notice.FileName??"":System.IO.Path.GetFileName(notice.Path),FontSize=12,Foreground=Brushes.White,
            TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(8),
            Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=4,Opacity=1}};
        var body=new Grid{Children={preview,filename}};Content=body;
        var interactionNotice=notice with{PreviewFrame=null};
        PointerPressed+=(_,args)=>
        {
            var point=args.GetCurrentPoint(this);var action=point.Properties.IsLeftButtonPressed?options.LeftAction:point.Properties.IsRightButtonPressed?options.RightAction:point.Properties.IsMiddleButtonPressed?options.MiddleAction:CompletionClickAction.None;
            if(action==CompletionClickAction.None)return;
            try{if(!CompletionActions.Execute(action,interactionNotice,Close,path=>Process.Start(new ProcessStartInfo(path){UseShellExecute=true})))ToolTip.SetTip(body,Loc.T("This result has no available file."));}
            catch(Exception error){DiagnosticLog.Write("Notification action: "+error.Message);ToolTip.SetTip(body,Loc.T("Unable to open the result."));}
            args.Handled=true;
        };
        timer.Tick+=(_,_)=>{Opacity=CompletionToastLayout.Opacity(elapsed.Elapsed,options);if(elapsed.Elapsed.TotalSeconds>=options.DurationSeconds)Close();};
        Closed+=(_,_)=>{timer.Stop();elapsed.Stop();previewCancellation.Cancel();preview.Source=null;previewBitmap?.Dispose();previewBitmap=null;};
        CompletionNotice? pendingPreview=notice;
        EventHandler? load=null;load=(_,_)=>{Opened-=load;var pending=pendingPreview;pendingPreview=null;if(pending is not null)LoadPreview(pending,options);};Opened+=load;
    }
    private async void LoadPreview(CompletionNotice notice,CompletionOptions options)
    {
        Avalonia.Media.Imaging.Bitmap? bitmap=null;
        try
        {
            if(notice.PreviewFrame is {} frame)bitmap=ShareVibeX.App.Capture.BitmapExportService.ToBitmap(frame);
            else if(notice.Path is {} path)bitmap=await ShareVibeX.App.Media.MediaThumbnailLoader.LoadAsync(path,notice.FfmpegPath,width:480,token:previewCancellation.Token);
            if(previewCancellation.IsCancellationRequested){bitmap?.Dispose();return;}
            if(bitmap is null){Close();return;}
            previewBitmap=bitmap;preview.Source=bitmap;
            var screen=Screens.Primary??Screens.All.FirstOrDefault();
            if(screen is not null)
            {
                var factor=Math.Min(options.Width/(double)bitmap.PixelSize.Width,options.Height/(double)bitmap.PixelSize.Height);
                var placement=CompletionToastLayout.Place(screen.WorkingArea,screen.Scaling,options with{Width=Math.Max(1,(int)Math.Round(bitmap.PixelSize.Width*factor)),Height=Math.Max(1,(int)Math.Round(bitmap.PixelSize.Height*factor))});
                Width=placement.Size.Width;Height=placement.Size.Height;Position=placement.Position;
            }
            Opacity=1;
        }
        catch(OperationCanceledException){bitmap?.Dispose();}
        catch(Exception error){bitmap?.Dispose();DiagnosticLog.Write("Completion preview: "+error.Message);}
        finally{if(!previewCancellation.IsCancellationRequested){elapsed.Restart();timer.Start();}}
    }
    public static IDisposable? TryShow(CompletionNotice notice,CompletionOptions options)
    {
        var toast=new CompletionToastWindow(notice,options);
        try
        {
            var screen=toast.Screens.Primary??toast.Screens.All.FirstOrDefault();
            if(screen is null){toast.Close();return null;}
            var placement=CompletionToastLayout.Place(screen.WorkingArea,screen.Scaling,options);
            toast.Position=placement.Position;toast.Width=placement.Size.Width;toast.Height=placement.Size.Height;
            // Do not show a notification at all if capture exclusion is unavailable.
            if(!WindowsCaptureExclusion.TryExclude(toast.TryGetPlatformHandle()?.Handle??0)){toast.Close();return null;}

            toast.Show();return toast;
        }
        catch{toast.Dispose();throw;}
    }
    public void Dispose(){previewCancellation.Cancel();timer.Stop();elapsed.Stop();Close();}
}
