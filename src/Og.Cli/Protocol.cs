using System.Text.Json;
using Og.Core.Runtime;

/// <summary>
/// The engine, driven by a program rather than a person.
///
///   og --protocol jsonl [--seed N] [--name NAME] WORLD.json
///
/// One command per line on stdin, as plain text. One JSON object per line on
/// stdout: everything the world said in reply, and every event the command
/// produced, so whoever is driving it can score what happened from the world's
/// own record rather than from the player's account of it.
///
/// The first line out is the arrival, before any command. A dice seed makes a
/// session repeatable from its commands; without one the dice are unseeded.
/// </summary>
internal static class Protocol
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static int Run(string worldPath, int seed, string name)
    {
        var world = World.Load(worldPath);
        var issues = WorldValidator.Validate(world);

        if (WorldValidator.HasErrors(issues))
        {
            Emit(new { type = "error", message = "world failed validation",
                       issues = issues.Select(i => i.ToString()).ToArray() });
            return 1;
        }

        var events = new CollectingEventSink();
        var engine = new GameEngine(world, events, seed);
        var output = new BufferSink();

        var died = false;
        engine.CharacterDied += _ => died = true;

        var session = engine.Enter(name, output);
        var seen = 0;
        Emit(Turn("arrive", null, output, events, ref seen, session, died));

        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            if (died)
            {
                Emit(new { type = "over", reason = "died" });
                continue;
            }

            engine.HandleInput(session, line);
            Emit(Turn("turn", line, output, events, ref seen, session, died));
        }

        return 0;
    }

    private static object Turn(string type, string? command, BufferSink output,
                               CollectingEventSink events, ref int seen, Session session, bool died)
    {
        var fresh = events.Events.Skip(seen).Select(e => new
        {
            type = e.Type,
            actor = e.Actor,
            character = e.CharacterId,
            location = e.LocationId,
            payload = e.Payload,
        }).ToArray();
        seen = events.Events.Count;

        return new
        {
            type,
            command,
            text = output.Drain(),
            events = fresh,
            alive = !died,
            location = died ? (int?)null : session.CurrentLocationId,
            hitPoints = died ? 0 : session.Character.HitPoints,
            // Reported every turn so the player need not ask for its inventory.
            inventory = died ? Array.Empty<string>() : session.Character.Inventory.Select(i => i.Name)
                .Concat(new[] { session.Character.Weapon?.Name, session.Character.Armor?.Name }.OfType<string>())
                .ToArray(),
        };
    }

    private static void Emit(object value)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(value, Json));
        Console.Out.Flush();
    }

    private sealed class BufferSink : IOutputSink
    {
        private readonly List<string> _lines = [];

        public void Write(string message) => _lines.Add(message);

        public string[] Drain()
        {
            var drained = _lines.ToArray();
            _lines.Clear();
            return drained;
        }
    }
}
