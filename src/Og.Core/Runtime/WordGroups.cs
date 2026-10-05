namespace Og.Core.Runtime;

/// <summary>
/// Named lists of words a trigger can refer to in braces: with
/// "give": ["give", "hand"], the trigger "{give} *&amp;*bread*" stands for
/// "give *&amp;*bread*" and "hand *&amp;*bread*". Several groups in one trigger
/// expand to every combination. A name with no group is left as written,
/// for the validator to report.
/// </summary>
public static class WordGroups
{
    public static string[] Expand(IEnumerable<string> triggers, IReadOnlyDictionary<string, string[]> words) =>
        [.. triggers.SelectMany(trigger => Expand(trigger, words)).Distinct()];

    public static IEnumerable<string> Expand(string trigger, IReadOnlyDictionary<string, string[]> words)
    {
        var open = trigger.IndexOf('{');
        var close = open < 0 ? -1 : trigger.IndexOf('}', open);

        if (close < 0 || !words.TryGetValue(trigger[(open + 1)..close], out var group))
        {
            return [trigger];
        }

        var before = trigger[..open];
        return group.SelectMany(word => Expand(before + word + trigger[(close + 1)..], words));
    }

    /// <summary>The group names a trigger still refers to after expansion: none, if every group exists.</summary>
    public static IEnumerable<string> Unresolved(string trigger)
    {
        for (var open = trigger.IndexOf('{'); open >= 0; open = trigger.IndexOf('{', open + 1))
        {
            var close = trigger.IndexOf('}', open);
            if (close < 0)
            {
                yield break;
            }

            yield return trigger[(open + 1)..close];
        }
    }
}
