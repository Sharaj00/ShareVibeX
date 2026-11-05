using Avalonia.Headless;

namespace ShareVibeX.App.Tests;

[CollectionDefinition("Avalonia UI")]
public sealed class UiCollection : ICollectionFixture<UiTestFixture>;

public sealed class UiTestFixture : IDisposable
{
    public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(HeadlessEntry));
    public Task Run(Func<Task> action) => Session.Dispatch(async () => { await action(); return true; },CancellationToken.None);
    public Task Run(Action action) => Session.Dispatch(action,CancellationToken.None);
    public void Dispose() => Session.Dispose();
}
