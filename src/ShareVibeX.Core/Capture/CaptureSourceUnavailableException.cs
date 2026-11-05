namespace ShareVibeX.Core.Capture;

public sealed class CaptureSourceUnavailableException : Exception
{
    public CaptureSourceUnavailableException(string message) : base(message) { }
    public CaptureSourceUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
