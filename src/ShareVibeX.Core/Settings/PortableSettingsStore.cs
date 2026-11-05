using ShareVibeX.Core.Platform;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using ShareVibeX.Core.IO;
using ShareVibeX.Core.Recording;

namespace ShareVibeX.Core.Settings;

public sealed class PortableSettingsStore(string dataDirectory,string? settingsPath=null) : ISettingsStore
{
    public string DataDirectory { get; } = Path.GetFullPath(dataDirectory);
    public string SettingsPath { get; } = Path.GetFullPath(settingsPath??Path.Combine(dataDirectory,"settings.json"));
    public string ExistingSettingsPath => File.Exists(SettingsPath)?SettingsPath:Path.Combine(DataDirectory,"settings.json");
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = ExistingSettingsPath;
        if (!File.Exists(path)) return AppSettings.FactoryDefaults;
        var node=JsonNode.Parse(await File.ReadAllTextAsync(path,cancellationToken)) as JsonObject
            ?? throw new InvalidDataException(Loc.T("Settings must be a JSON object."));
        var schema=node["SchemaVersion"]?.GetValue<int>()??1;
        if(schema==1)
        {
            var legacy=node["Theme"]?.ToJsonString().ToLowerInvariant()??"\"dark\"";
            var theme=legacy switch {"\"light\"" or "\"dark\"" or "0" or "1"=>ThemeMode.System,"\"amoled\"" or "2"=>ThemeMode.Contrast,_=>throw new InvalidDataException(Loc.T("Unknown legacy theme."))};
            node["SchemaVersion"]=2;node["Theme"]=theme.ToString();
        }
        else if(schema!=2)throw new InvalidDataException(Loc.T("Settings use an unsupported version."));
        var settings=node.Deserialize<AppSettings>(Options);
        if(settings?.RecordingDefaults is {Encoder:VideoEncoder.LibvpxVp9,Container:VideoContainer.Mp4} legacyVp9)
            settings=settings with{Recording=legacyVp9 with{Container=VideoContainer.Matroska}};
        Validate(settings);
        return settings! with{TraySingleClick=CurrentAction(settings!.TraySingleClick),TrayDoubleClick=CurrentAction(settings.TrayDoubleClick),TrayMiddleClick=CurrentAction(settings.TrayMiddleClick)};
    }
    private static TrayAction CurrentAction(TrayAction value)=>value==TrayAction.OpenHistory?TrayAction.OpenSettings:value;

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Validate(settings);
        return AtomicFile.WriteAsync(SettingsPath,
            (stream, token) => JsonSerializer.SerializeAsync(stream, settings, Options, token), cancellationToken);
    }

    private static void Validate(AppSettings? settings)
    {
        if (settings is null || settings.SchemaVersion != 2 || !Enum.IsDefined(settings.CaptureBackend) || !Enum.IsDefined(settings.Language) || !Enum.IsDefined(settings.TraySingleClick) || !Enum.IsDefined(settings.TrayDoubleClick) || !Enum.IsDefined(settings.TrayMiddleClick) || !Enum.IsDefined(settings.Theme) || !Enum.IsDefined(settings.ImageFormat) || settings.ImageQuality is <1 or >100 || settings.ButtonSize is <24 or >64 || settings.RecordingCountdownSeconds is <0 or >10 ||
            string.IsNullOrWhiteSpace(settings.CaptureHotkey))
            throw new InvalidDataException(Loc.T("Settings are invalid or use an unsupported version. Restore a valid settings file or use Back up and reset settings."));
        var completion=settings.CompletionDefaults;
        if(!Enum.IsDefined(completion.LeftAction) || !Enum.IsDefined(completion.RightAction) || !Enum.IsDefined(completion.MiddleAction) || completion.DurationSeconds is <1 or >30 || !double.IsFinite(completion.FadeSeconds) || completion.FadeSeconds<0 || completion.FadeSeconds>5 || completion.FadeSeconds>completion.DurationSeconds || completion.Width is <240 or >800 || completion.Height is <100 or >400 || !Enum.IsDefined(completion.Placement))
            throw new InvalidDataException(Loc.T("Completion notification settings are invalid."));
        try { ShareVibeX.Core.Capture.SelectionRules.Validate(settings.SelectionDefaults); }
        catch(ArgumentException error){throw new InvalidDataException(error.Message,error);}
        try { RecordingValidation.Validate(settings.RecordingDefaults); }
        catch (ArgumentException error) { throw new InvalidDataException(Loc.T("Recording settings are invalid: ") + error.Message, error); }
    }
}


