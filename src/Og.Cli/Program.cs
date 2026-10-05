using Og.Core.Runtime;

if (args.Contains("--protocol"))
{
    string Option(string flag, string fallback)
    {
        var at = Array.IndexOf(args, flag);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : fallback;
    }

    var flagged = new HashSet<string> { "--protocol", "--seed", "--name" };
    var positional = args.Where((value, i) => !flagged.Contains(value)
        && (i == 0 || !flagged.Contains(args[i - 1]))).ToArray();

    if (Option("--protocol", "") != "jsonl" || positional.Length != 1 || !File.Exists(positional[0]))
    {
        Console.Error.WriteLine("usage: og --protocol jsonl [--seed N] [--name NAME] WORLD.json");
        return 2;
    }

    return Protocol.Run(Path.GetFullPath(positional[0]),
        int.Parse(Option("--seed", "0")), Option("--name", "wanderer"));
}

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: og WORLD.json | og --protocol jsonl [--seed N] [--name NAME] WORLD.json");
    return 2;
}

var worldPath = Path.GetFullPath(args[0]);

if (!File.Exists(worldPath))
{
    Console.Error.WriteLine($"World file not found: {worldPath}");
    return 1;
}

var world = World.Load(worldPath);
var issues = WorldValidator.Validate(world);

foreach (var issue in issues)
{
    Console.Error.WriteLine(issue.ToString());
}

if (WorldValidator.HasErrors(issues))
{
    Console.Error.WriteLine("World failed validation.");
    return 1;
}

var events = new CollectingEventSink();
var engine = new GameEngine(world, events);
var output = new ConsoleOutputSink();

var session = engine.Enter(AskName("Your name"), output);
var lives = 1;

// A death ends that character for good. Playing on means starting a new life,
// which the world has no way of connecting to the one before it.
engine.CharacterDied += _ => session = null!;

while (true)
{
    if (session is null)
    {
        Console.Write("\nPlay on as someone new? (y/n) ");

        if (Console.ReadLine()?.Trim().ToLowerInvariant() is not ("y" or "yes" or ""))
        {
            break;
        }

        session = engine.Enter(AskName("Their name"), output);
        lives++;
        continue;
    }

    Console.Write("\n> ");
    var line = Console.ReadLine();

    if (line is null || line.Trim() is "quit" or "q")
    {
        engine.Depart(session);
        break;
    }

    engine.HandleInput(session, line);
}

Console.WriteLine($"\n{lives} life/lives lived, {events.Events.Count} events recorded.");
return 0;

static string AskName(string prompt)
{
    Console.Write($"{prompt}: ");
    var name = Console.ReadLine()?.Trim();
    return string.IsNullOrWhiteSpace(name) ? "wanderer" : name;
}

internal sealed class ConsoleOutputSink : IOutputSink
{
    public void Write(string message) => Console.WriteLine(message);
}
