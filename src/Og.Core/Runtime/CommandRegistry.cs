namespace Og.Core.Runtime;

public delegate CommandResult CommandHandler(GameEngine engine, Session session, string argument);

/// <summary>
/// The verbs the engine itself knows. Registered rather than hardcoded, so new
/// ones can be added without editing this file.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandHandler> _exact = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Prefix, CommandHandler Handler)> _prefixes = [];

    public void RegisterExact(CommandHandler handler, params string[] forms)
    {
        foreach (var form in forms)
        {
            _exact[form] = handler;
        }
    }

    public void RegisterPrefix(CommandHandler handler, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            _prefixes.Add((prefix, handler));
        }
    }

    /// <summary>
    /// Matches on the normalised command but hands the handler the argument as the
    /// player actually typed it, so speech keeps its punctuation and capitals.
    /// </summary>
    public CommandResult Execute(GameEngine engine, Session session, string command, string raw)
    {
        if (_exact.TryGetValue(command, out var exactHandler))
        {
            return exactHandler(engine, session, "");
        }

        foreach (var (prefix, handler) in _prefixes.OrderByDescending(entry => entry.Prefix.Length))
        {
            if (!command.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var argument = raw.Length > prefix.Length && raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? raw[prefix.Length..].Trim()
                : command[(prefix.Length + 1)..].Trim();

            return handler(engine, session, argument);
        }

        return CommandResult.NotHandled;
    }
}
