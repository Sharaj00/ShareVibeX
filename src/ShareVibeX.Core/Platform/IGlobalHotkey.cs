namespace ShareVibeX.Core.Platform;

public interface IGlobalHotkey : IDisposable
{
    void Register(string gesture, Action onTriggered);
    void Unregister();
}
