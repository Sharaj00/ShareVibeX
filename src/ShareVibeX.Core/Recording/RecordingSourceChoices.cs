namespace ShareVibeX.Core.Recording;

public readonly record struct RecordingSourceChoices(bool Sound, bool Microphone)
{
    public bool WithAudio => Sound || Microphone;
    public static RecordingSourceChoices FromOptions(RecordingOptions options) => options.IsAnimation?new(false,false):options.AudioSource switch
    {
        AudioSource.Microphone => new(false,true),
        AudioSource.SystemAndMicrophone => new(true,true),
        _ => new(true,false)
    };
    public RecordingOptions Apply(RecordingOptions options)
    {
        if(options.AudioSource==AudioSource.DirectShow && Sound && Microphone)
            throw new NotSupportedException(Loc.T("DirectShow and a native microphone cannot be mixed here. Select a native playback source in Video settings."));
        return options with { AudioSource = Sound && options.AudioSource==AudioSource.DirectShow ? AudioSource.DirectShow :
            Sound && Microphone ? AudioSource.SystemAndMicrophone : Microphone ? AudioSource.Microphone : AudioSource.System };
    }
}
