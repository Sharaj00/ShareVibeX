using System.Text;

namespace ShareVibeX.Platform.Windows;

public static class CompletionSoundFile
{
    public const long MaximumBytes=16*1024*1024;
    public static string? TryResolve(string? path,string baseDirectory)
    {
        if(string.IsNullOrWhiteSpace(path))return null;
        try
        {
            var resolved=Path.GetFullPath(path,Path.GetFullPath(baseDirectory));
            if(!string.Equals(Path.GetExtension(resolved),".wav",StringComparison.OrdinalIgnoreCase))return null;
            using var stream=new FileStream(resolved,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length<44||stream.Length>MaximumBytes)return null;
            using var reader=new BinaryReader(stream,Encoding.ASCII,leaveOpen:true);
            if(reader.ReadUInt32()!=0x46464952)return null;
            var end=8L+reader.ReadUInt32();if(end>stream.Length||end<44||reader.ReadUInt32()!=0x45564157)return null;
            var format=false;var samples=false;
            for(var count=0;stream.Position+8<=end&&count<4096;count++)
            {
                var id=reader.ReadUInt32();var size=reader.ReadUInt32();var next=stream.Position+size+(size&1);
                if(next>end)return null;
                if(id==0x20746d66)
                {
                    if(size<16)return null;
                    var encoding=reader.ReadUInt16();var channels=reader.ReadUInt16();var sampleRate=reader.ReadUInt32();
                    var byteRate=reader.ReadUInt32();var blockAlign=reader.ReadUInt16();var bits=reader.ReadUInt16();
                    format=(encoding is 1 or 3 or 0xfffe) && (encoding!=0xfffe||size>=40) && (channels is >0 and <=8) && (sampleRate is >0 and <=384000) && byteRate>0&&blockAlign>0&&bits>0;
                }
                else if(id==0x61746164)samples=size>0;
                if(format&&samples)return resolved;
                stream.Position=next;
            }
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException){ }
        return null;
    }
}
