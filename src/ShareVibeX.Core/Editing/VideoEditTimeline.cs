using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ShareVibeX.Core.Editing;

public readonly record struct VideoFrameSpan(long Start,long End){public long Length=>End-Start;}
public sealed class VideoEditTimeline
{
    public long OriginalFrames {get;}
    public int FrameRate {get;}
    public IReadOnlyList<VideoFrameSpan> Spans {get;}
    public long FrameCount {get;}
    public TimeSpan Duration=>TimeSpan.FromSeconds(FrameCount/(double)FrameRate);
    public bool IsOriginal=>Spans.Count==1&&Spans[0]==new VideoFrameSpan(0,OriginalFrames);
    public string Fingerprint {get;}
    public VideoEditTimeline(long frames,int rate):this(frames,rate,[new(0,frames)]){}
    public VideoEditTimeline(long frames,int rate,IEnumerable<VideoFrameSpan> spans)
    {
        if(frames<=0||rate is <1 or >240||frames/(double)rate>TimeSpan.MaxValue.TotalSeconds)throw new ArgumentOutOfRangeException(nameof(frames));
        OriginalFrames=frames;FrameRate=rate;var normalized=new List<VideoFrameSpan>();
        foreach(var span in spans)
        {
            if(span.Start<0||span.End<=span.Start||span.End>frames||normalized.Count>0&&span.Start<normalized[^1].End)throw new ArgumentException(Loc.T("Invalid source-frame intervals."));
            if(normalized.Count>0&&normalized[^1].End==span.Start)normalized[^1]=new(normalized[^1].Start,span.End);else normalized.Add(span);
        }
        if(normalized.Count==0)throw new ArgumentException(Loc.T("The edit must retain at least one video frame."));
        Spans=normalized.AsReadOnly();FrameCount=normalized.Sum(s=>s.Length);
        var text=frames.ToString(CultureInfo.InvariantCulture)+"/"+rate.ToString(CultureInfo.InvariantCulture)+":"+string.Join(';',normalized.Select(s=>s.Start.ToString(CultureInfo.InvariantCulture)+"-"+s.End.ToString(CultureInfo.InvariantCulture)));
        Fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
    public long Snap(TimeSpan position)=>Math.Clamp((long)Math.Round(Math.Clamp(position.TotalSeconds,0,Duration.TotalSeconds)*FrameRate,MidpointRounding.AwayFromZero),0,FrameCount);
    public VideoEditTimeline Keep(long a,long b){Validate(a,b);return new(OriginalFrames,FrameRate,Slice(a,b));}
    public VideoEditTimeline Remove(long a,long b)
    {Validate(a,b);if(a==0&&b==FrameCount)throw new ArgumentException(Loc.T("Removing the entire video is not allowed."));return new(OriginalFrames,FrameRate,Slice(0,a).Concat(Slice(b,FrameCount)));}
    public VideoEditTimeline Reset()=>new(OriginalFrames,FrameRate);
    private void Validate(long a,long b){if(a<0||b<=a||b>FrameCount)throw new ArgumentException(Loc.T("Select a nonempty A–B interval inside the current video."));}
    private IEnumerable<VideoFrameSpan> Slice(long a,long b)
    {
        long offset=0;
        foreach(var span in Spans)
        {
            var start=Math.Max(a,offset);var end=Math.Min(b,offset+span.Length);
            if(start<end)yield return new(span.Start+start-offset,span.Start+end-offset);offset+=span.Length;
        }
    }
}
