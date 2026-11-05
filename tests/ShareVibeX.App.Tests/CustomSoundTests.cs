using ShareVibeX.Platform.Windows;

namespace ShareVibeX.App.Tests;

public sealed class CustomSoundTests
{
    [Fact]
    public void RelativePcmWaveResolvesAgainstPortableDirectory()
    {
        var root=CreateDirectory();
        try
        {
            var path=Path.Combine(root,"complete.wav");File.WriteAllBytes(path,PcmWave());
            Assert.Equal(path,CompletionSoundFile.TryResolve("complete.wav",root));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void MissingUnsupportedAndTruncatedFilesFallBackToSystemSound()
    {
        var root=CreateDirectory();
        try
        {
            Assert.Null(CompletionSoundFile.TryResolve(null,root));
            Assert.Null(CompletionSoundFile.TryResolve("missing.wav",root));
            File.WriteAllBytes(Path.Combine(root,"bad.wav"),new byte[12]);
            Assert.Null(CompletionSoundFile.TryResolve("bad.wav",root));
            File.WriteAllBytes(Path.Combine(root,"sound.mp3"),PcmWave());
            Assert.Null(CompletionSoundFile.TryResolve("sound.mp3",root));
            File.WriteAllBytes(Path.Combine(root,"truncated.wav"),PcmWave()[..40]);
            Assert.Null(CompletionSoundFile.TryResolve("truncated.wav",root));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void OversizedAndInvalidChunkLengthsAreRejected()
    {
        var root=CreateDirectory();
        try
        {
            var path=Path.Combine(root,"large.wav");using(var file=File.Create(path)){file.Write(PcmWave());file.SetLength(CompletionSoundFile.MaximumBytes+1);}
            Assert.Null(CompletionSoundFile.TryResolve(path,root));
            var bytes=PcmWave();BitConverter.GetBytes(uint.MaxValue).CopyTo(bytes,40);File.WriteAllBytes(path,bytes);
            Assert.Null(CompletionSoundFile.TryResolve(path,root));
        }
        finally{Directory.Delete(root,true);}
    }
    private static string CreateDirectory(){var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-sound-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;}
    private static byte[] PcmWave()
    {
        using var memory=new MemoryStream();using var writer=new BinaryWriter(memory);
        writer.Write("RIFF"u8);writer.Write(38);writer.Write("WAVEfmt "u8);writer.Write(16);writer.Write((ushort)1);writer.Write((ushort)1);
        writer.Write(8000);writer.Write(16000);writer.Write((ushort)2);writer.Write((ushort)16);writer.Write("data"u8);writer.Write(2);writer.Write((short)0);return memory.ToArray();
    }
}
