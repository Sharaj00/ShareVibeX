using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ShareVibeX.App.Tests")]

namespace ShareVibeX.Platform.Windows.Recording;

internal sealed class RecordingAudioBuffer(int capacity)
{
    private readonly float[] samples = new float[capacity];
    private readonly object gate = new();
    private int head;
    private int count;
    public void Clear(){lock(gate){head=0;count=0;Array.Clear(samples);}}

    public void Write(ReadOnlySpan<float> packet)
    {
        lock (gate)
        {
            if (packet.Length % 2 != 0) throw new ArgumentException(Loc.T("Audio packets must contain complete stereo frames."), nameof(packet));
            if (packet.Length > samples.Length - count)
                throw new InvalidOperationException(Loc.T("The audio buffer overflowed. The audio device clock or recording output is not keeping pace."));
            foreach (var sample in packet) samples[(head + count++) % samples.Length] = sample;
        }
    }

    private void AddTo(Span<float> destination)
    {
        lock (gate)
        {
            var available = Math.Min(destination.Length, count);
            for (var i = 0; i < available; i++)
            {
                destination[i] += samples[head];
                head = (head + 1) % samples.Length;
            }
            count -= available;
        }
    }

    public static void Mix(IReadOnlyList<RecordingAudioBuffer> buffers, Span<float> destination)
    {
        destination.Clear();
        foreach (var buffer in buffers) buffer.AddTo(destination);
        for (var i = 0; i < destination.Length; i++) destination[i] = Math.Clamp(destination[i], -1f, 1f);
    }
}
