using System.Globalization;
using System.Text.RegularExpressions;
using ShareVibeX.Core.Localization;

namespace ShareVibeX.Core.Tests;

public sealed class LocalizationCatalogTests
{
    [Fact]
    public void ExplicitResourcesCoverRussianWithoutChangingEnglish()
    {
        Assert.True(Loc.ResourceKeys.Count>500);
        Assert.Equal("Настройки",Loc.T("Settings",AppLanguage.Russian));
        Assert.Equal("Settings",Loc.T("Settings",AppLanguage.English));
        foreach(var key in Loc.ResourceKeys)
        {
            var translated=Loc.T(key,AppLanguage.Russian);Assert.False(string.IsNullOrWhiteSpace(translated));
            var originalFields=Regex.Matches(key,@"\{\d+(?::[^}]*)?\}").Select(m=>m.Value).Order().ToArray();
            var translatedFields=Regex.Matches(translated,@"\{\d+(?::[^}]*)?\}").Select(m=>m.Value).Order().ToArray();
            Assert.Equal(originalFields,translatedFields);
        }
    }
    [Fact]
    public void UserPathsAndTechnicalArgumentsRemainUnmodified()
    {
        const string path=@"C:\Settings\Capture history_20261103.mp4";
        Assert.Equal(path,Loc.T(path,AppLanguage.Russian));
        Assert.Equal("-vf scale=640:480 -c:v libx264",Loc.T("-vf scale=640:480 -c:v libx264",AppLanguage.Russian));
        var message=string.Format(CultureInfo.InvariantCulture,Loc.T("Tray error: {0}",AppLanguage.Russian),path);
        Assert.Equal("Ошибка значка трея: "+path,message);
    }
}
