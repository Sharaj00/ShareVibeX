using System.Buffers.Binary;
using NAudio.Wave;

namespace ShareVibeX.Platform.Windows.Recording;

// Streaming linear conversion keeps fractional position and the previous frame across callbacks.
internal sealed class RecordingAudioConverter
{
    private readonly WaveFormat format;
    private readonly double step;
    private long frameIndex = -1;
    private double nextOutput;
    private float previousLeft;
    private float previousRight;

    public RecordingAudioConverter(WaveFormat format)
    {
        this.format = format;
        // NAudio's standard-format getter removes the extensible speaker mask. Reject
        // surround rather than guessing its channel order and losing center dialogue.
        if (format.Channels is < 1 or > 2)
            throw new NotSupportedException(Loc.T("Windows recording currently supports mono or stereo audio endpoints. Configure this endpoint as stereo in Windows sound settings."));
        if (format.SampleRate <= 0 ||
            !(format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32 ||
              format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample is 16 or 24 or 32))
            throw new NotSupportedException(Loc.F("Unsupported native audio format: {0}.",format));
        step = format.SampleRate / 48000d;
    }

    public void Convert(ReadOnlySpan<byte> bytes, Action<float> write)
    {
        if (bytes.Length % format.BlockAlign != 0) throw new InvalidOperationException(Loc.T("An incomplete native audio frame was received."));
        for (var offset = 0; offset < bytes.Length; offset += format.BlockAlign)
        {
            var frame = bytes.Slice(offset, format.BlockAlign);
            var left = Decode(frame, 0);
            var right = format.Channels == 1 ? left : Decode(frame, 1);
            frameIndex++;
            while (nextOutput <= frameIndex)
            {
                var fraction = frameIndex == 0 ? 1 : (float)(nextOutput - (frameIndex - 1));
                write(previousLeft + (left - previousLeft) * fraction);
                write(previousRight + (right - previousRight) * fraction);
                nextOutput += step;
            }
            previousLeft = left;
            previousRight = right;
        }
    }

    private float Decode(ReadOnlySpan<byte> frame, int channel)
    {
        var value = frame.Slice(channel * (format.BitsPerSample / 8));
        float sample = format.Encoding == WaveFormatEncoding.IeeeFloat
            ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(value))
            : format.BitsPerSample switch
            {
                16 => BinaryPrimitives.ReadInt16LittleEndian(value) / 32768f,
                24 => ((value[0] | value[1] << 8 | value[2] << 16) << 8 >> 8) / 8388608f,
                _ => BinaryPrimitives.ReadInt32LittleEndian(value) / 2147483648f
            };
        if (!float.IsFinite(sample)) throw new InvalidOperationException(Loc.T("The audio device returned a non-finite sample."));
        return sample;
    }
}
