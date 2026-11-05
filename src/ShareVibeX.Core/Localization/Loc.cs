using System.Globalization;
using System.Text.Json;

namespace ShareVibeX.Core.Localization;

public enum AppLanguage { English, Russian }

public static class Loc
{
    private static readonly IReadOnlyDictionary<string,string> Russian = Load();
    public static AppLanguage Language {get;set;}=AppLanguage.English;
    public static string T(string text)=>T(text,Language);
    public static string T(string text,AppLanguage language)=>language==AppLanguage.Russian&&Russian.TryGetValue(text,out var translated)?translated:text;
    public static string F(string format,params object?[] values)=>string.Format(CultureInfo.InvariantCulture,T(format),values);
    public static IReadOnlyCollection<string> ResourceKeys=>Russian.Keys.ToArray();
    private static IReadOnlyDictionary<string,string> Load()
    {
        using var stream=typeof(Loc).Assembly.GetManifestResourceStream("ShareVibeX.Core.Localization.ru.json")??throw new InvalidOperationException("Localization catalog is unavailable.");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidOperationException("Localization catalog is empty.");
    }
}
