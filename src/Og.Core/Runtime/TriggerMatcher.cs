namespace Og.Core.Runtime;

/// <summary>
/// Glob-style: "*w*" contains, "*w" ends with, "w*" starts with, bare is exact.
/// Parts joined by "&amp;" must all match, e.g. "give*&amp;*bread*".
/// </summary>
public static class TriggerMatcher
{
    private static readonly char[] Punctuation = ['.', '!', '?', ','];

    public static bool Matches(string trigger, string command)
    {
        if (trigger.Contains('&'))
        {
            var parts = trigger.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length > 0 && parts.All(part => Matches(part, command));
        }

        var text = Normalize(command);
        var needle = trigger.Replace("*", "").Trim().ToLowerInvariant();

        if (needle.Length == 0)
        {
            return false;
        }

        var leading = trigger.StartsWith('*');
        var trailing = trigger.EndsWith('*');

        return (leading, trailing) switch
        {
            (true, true) => text.Contains(needle, StringComparison.Ordinal),
            (true, false) => text.EndsWith(needle, StringComparison.Ordinal),
            (false, true) => text.StartsWith(needle, StringComparison.Ordinal),
            _ => text == needle
        };
    }

    public static string Normalize(string command)
    {
        var text = command.Trim().ToLowerInvariant();

        foreach (var character in Punctuation)
        {
            text = text.Replace(character.ToString(), "");
        }

        return text.Trim();
    }
}
