using NAudio.Wave;
using ShareVibeX.Platform.Windows.Recording;

namespace ShareVibeX.App.Tests;

public sealed class RecordingAudioTests
{
    [Fact]
    public void PcmMonoConversionDuplicatesChannelsAndResamplesContinuouslyAcrossPackets()
    {
        var converter = new RecordingAudioConverter(new WaveFormat(24000, 16, 1));
        var samples = new List<float>();
        converter.Convert([0, 0, 0, 64], samples.Add);
        converter.Convert([0, 0], samples.Add);
        Assert.Equal(new float[] { 0, 0, .25f, .25f, .5f, .5f, .25f, .25f, 0, 0 }, samples);
    }

    [Fact]
    public void EmptyMixProducesSilenceAndAvailableSourcesAreLimitedWithoutClipping()
    {
        var first = new RecordingAudioBuffer(8);
        var second = new RecordingAudioBuffer(8);
        var output = new float[4];
        RecordingAudioBuffer.Mix([first, second], output);
        Assert.Equal(new float[4], output);
        first.Write([.8f, -.8f]);
        second.Write([.8f, -.8f]);
        RecordingAudioBuffer.Mix([first, second], output);
        Assert.Equal(new float[] { 1, -1, 0, 0 }, output);
    }

    [Fact]
    public void BufferOverflowFailsExplicitlyInsteadOfDroppingSamples()
    {
        var buffer = new RecordingAudioBuffer(2);
        buffer.Write([.1f, .2f]);
        Assert.Throws<InvalidOperationException>(() => buffer.Write([.3f, .4f]));
    }

    [Fact]
    public void RejectedPacketDoesNotPartiallyChangeQueuedChannelPairs()
    {
        var buffer = new RecordingAudioBuffer(4);
        buffer.Write([.1f, .2f]);
        Assert.Throws<InvalidOperationException>(() => buffer.Write([.3f, .4f, .5f, .6f]));
        var output = new float[4];
        RecordingAudioBuffer.Mix([buffer], output);
        Assert.Equal(new float[] { .1f, .2f, 0, 0 }, output);
    }

    [Fact]
    public void FloatStereoConversionPreservesChannelOrder()
    {
        var converter = new RecordingAudioConverter(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
        byte[] bytes = new byte[16];
        Buffer.BlockCopy(new float[] { .25f, -.5f, .75f, -1 }, 0, bytes, 0, 16);
        var samples = new List<float>();
        converter.Convert(bytes, samples.Add);
        Assert.Equal(new float[] { .25f, -.5f, .75f, -1 }, samples);
    }

    [Fact]
    public void SurroundEndpointFailsClearlyRatherThanLosingCenterDialogue()
    {
        var error = Assert.Throws<NotSupportedException>(() =>
            new RecordingAudioConverter(WaveFormat.CreateIeeeFloatWaveFormat(48000, 6)));
        Assert.Contains("mono or stereo", error.Message);
    }
}
