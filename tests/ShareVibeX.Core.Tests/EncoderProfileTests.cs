using ShareVibeX.Core.Recording;
using ShareVibeX.Core.Settings;

namespace ShareVibeX.Core.Tests;

public sealed class EncoderProfileTests
{
    [Fact] public void OlderProfileJsonKeepsAnimationDefaults()
    {
        var profile=System.Text.Json.JsonSerializer.Deserialize<VideoEncoderProfile>("{\"RateControl\":0,\"Quality\":23,\"BitrateKbps\":8000,\"Preset\":\"auto\",\"Tune\":\"\",\"AdditionalArguments\":\"\"}");
        Assert.NotNull(profile);Assert.Equal(4,profile.AnimationCompressionLevel);Assert.Equal(0,profile.AnimationLoopCount);Assert.False(profile.AnimationLossless);
    }
    [Fact] public void SwitchingRestoresIndependentValuesWithoutMutatingOriginal()
    {
        var original=new RecordingOptions{Quality=18,Preset="placebo",Tune="film",AdditionalArguments="-profile:v high",ScalePercent=50};
        var hardware=original.SwitchEncoder(VideoEncoder.H264Nvenc);
        Assert.Equal(23,hardware.Quality);Assert.Equal("auto",hardware.Preset);Assert.Equal("",hardware.Tune);Assert.Equal("",hardware.AdditionalArguments);
        hardware=hardware with{Quality=30,Preset="p6",Tune="hq",RateControl=VideoRateControl.Bitrate,VideoBitrateKbps=12000};
        var software=hardware.SwitchEncoder(VideoEncoder.Libx264);
        Assert.Equal(VideoEncoderProfile.From(original),VideoEncoderProfile.From(software));Assert.Equal(50,software.ScalePercent);
        var again=software.SwitchEncoder(VideoEncoder.H264Nvenc);
        Assert.Equal(VideoEncoderProfile.From(hardware),VideoEncoderProfile.From(again));Assert.Null(original.EncoderProfiles);
        RecordingValidation.Validate(again);
    }
    [Fact] public async Task ProfilesSurvivePortableSaveAndLoad()
    {
        var root=Path.Combine(Path.GetTempPath(),"ShareVibeX-profiles",Guid.NewGuid().ToString("N"));
        try
        {
            var options=new RecordingOptions{Quality=17,Tune="film"}.SwitchEncoder(VideoEncoder.H264Nvenc) with{Preset="p7",Quality=29};
            var store=new PortableSettingsStore(root);await store.SaveAsync(new AppSettings(Recording:options));
            var loaded=(await store.LoadAsync()).RecordingDefaults;
            Assert.Equal(17,loaded.SwitchEncoder(VideoEncoder.Libx264).Quality);
            Assert.Equal("film",loaded.SwitchEncoder(VideoEncoder.Libx264).Tune);
            Assert.Equal(29,loaded.SwitchEncoder(VideoEncoder.Libx264).SwitchEncoder(VideoEncoder.H264Nvenc).Quality);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact] public void UnknownAndInvalidInactiveProfilesAreRejected()
    {
        var profile=VideoEncoderProfile.From(new());
        Assert.Throws<ArgumentException>(()=>RecordingValidation.Validate(new(){EncoderProfiles=new Dictionary<VideoEncoder,VideoEncoderProfile>{[(VideoEncoder)999]=profile}}));
        Assert.Throws<ArgumentException>(()=>RecordingValidation.Validate(new(){EncoderProfiles=new Dictionary<VideoEncoder,VideoEncoderProfile>{[VideoEncoder.H264Nvenc]=profile with{Preset="placebo"}}}));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new RecordingOptions().SwitchEncoder((VideoEncoder)999));
    }
    [Fact] public void LegacyOptionsKeepCurrentValuesAndNoSwitchIsAnIdentity()
    {
        var options=new RecordingOptions{Encoder=VideoEncoder.H264Qsv,Quality=15,Preset="slow"};
        Assert.Same(options,options.SwitchEncoder(VideoEncoder.H264Qsv));
        Assert.Equal(VideoEncoderProfile.From(options),VideoEncoderProfile.From(options.SwitchEncoder(VideoEncoder.Libx264).SwitchEncoder(VideoEncoder.H264Qsv)));
    }
}
