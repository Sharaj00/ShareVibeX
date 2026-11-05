using System.Text;

namespace ShareVibeX.Core.Recording;

public static class FfmpegArguments
{
    public static IReadOnlyList<string> Parse(string text)
    {
        var result = new List<string>();
        var value = new StringBuilder();
        char quote = '\0'; var started = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\0' || ch is '\r' or '\n') throw new ArgumentException(Loc.T("Arguments must be a single line without null characters."));
            if (ch == '\\' && quote != '\'')
            {
                var first = i;
                while (i + 1 < text.Length && text[i + 1] == '\\') i++;
                var slashes = i - first + 1;
                if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    value.Append('\\', slashes / 2); i++;
                    if (slashes % 2 == 1) value.Append('"');
                    else quote = quote == '"' ? '\0' : '"';
                }
                else value.Append('\\', slashes);
                started = true; continue;
            }
            if (ch is '"' or '\'' && (quote == '\0' || quote == ch))
            { quote = quote == '\0' ? ch : '\0'; started = true; continue; }
            if (char.IsWhiteSpace(ch) && quote == '\0')
            {
                if (started) { result.Add(value.ToString()); value.Clear(); started = false; }
            }
            else { value.Append(ch); started = true; }
        }
        if (quote != '\0') throw new ArgumentException(Loc.T("Close the quoted argument."));
        if (started) result.Add(value.ToString());
        return result;
    }

    // Preview only. Execution always uses ProcessStartInfo.ArgumentList.
    public static string Display(IEnumerable<string> values) => string.Join(" ", values.Select(Quote));
    private static string Quote(string value)
    {
        if (value.Length > 0 && !value.Any(ch => char.IsWhiteSpace(ch) || ch is '"' or '\'')) return value;
        var result = new StringBuilder("\""); var slashes = 0;
        foreach (var ch in value)
        {
            if (ch == '\\') { slashes++; continue; }
            result.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes); slashes = 0;
            result.Append(ch);
        }
        result.Append('\\', slashes * 2); return result.Append('"').ToString();
    }
}
