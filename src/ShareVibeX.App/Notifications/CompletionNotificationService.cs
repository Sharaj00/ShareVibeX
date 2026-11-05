using Avalonia.Threading;
using ShareVibeX.Core.Settings;
using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Notifications;

public sealed record CompletionNotice(string Title,string? Path=null,ShareVibeX.Core.Capture.CapturedFrame? PreviewFrame=null,string? FfmpegPath=null,string? FileName=null);

public sealed class CompletionNotificationService:IDisposable
{
    private readonly Func<CompletionOptions> options;
    private readonly Func<bool> fullscreen;
    private readonly Action sound,stopSound;
    private readonly Func<CompletionNotice,CompletionOptions,IDisposable?> show;
    private readonly Action<Action> dispatch;
    private IDisposable? current;
    private volatile bool blocked,disposed;private long generation;
    public CompletionNotificationService(Func<CompletionOptions> options,Func<bool>? fullscreen=null,Action? sound=null,
        Func<CompletionNotice,CompletionOptions,IDisposable?>? show=null,Action<Action>? dispatch=null,Action? stopSound=null)
    {
        this.options=options;this.fullscreen=fullscreen??WindowsCompletionPlatform.IsExclusiveFullscreenForeground;
        this.sound=sound??(()=>WindowsCompletionPlatform.PlayCompletionSound(options().CustomSoundPath));
        this.stopSound=stopSound??(sound is null?(Action)WindowsCompletionPlatform.StopCompletionSound:()=>{});this.show=show??CompletionToastWindow.TryShow;
        this.dispatch=dispatch??(action=>{if(Dispatcher.UIThread.CheckAccess())action();else Dispatcher.UIThread.Post(action);});
    }
    public void Publish(CompletionNotice notice)
    {
        if(disposed||blocked)return;var revision=Interlocked.Read(ref generation);
        Run(()=>
        {
            if(disposed||blocked||revision!=Interlocked.Read(ref generation))return;
            CloseCurrent();var settings=options();
            if(!settings.ShowNotifications&&!settings.PlaySound)return;
            if(settings.SuppressFullscreen&&fullscreen())return;
            if(settings.ShowNotifications)
            {try{current=show(notice,settings);}catch(Exception error){Log(error);}}
            if(settings.PlaySound)
            {try{sound();}catch(Exception error){Log(error);}}
        });
    }
    public void Block(){blocked=true;Interlocked.Increment(ref generation);Run(StopFeedback);}
    public void Unblock(){if(!disposed)blocked=false;}
    public void Dispose(){disposed=true;Interlocked.Increment(ref generation);Run(StopFeedback);}
    private void Run(Action action)
    {
        try{dispatch(()=>{try{action();}catch(Exception error){Log(error);}});}catch(Exception error){Log(error);}
    }
    private void StopFeedback(){CloseCurrent();try{stopSound();}catch(Exception error){Log(error);}}
    private void CloseCurrent(){var previous=current;current=null;try{previous?.Dispose();}catch(Exception error){Log(error);}}
    private static void Log(Exception error)=>DiagnosticLog.Write("Completion notification: "+error.Message);
}
