using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Og.Core.Runtime;

// The engine in the browser: one game at a time, driven the way the harness
// drives Og.Cli, and answering each command with the same JSON.
return;

public static partial class Game
{
    private static GameEngine? _engine;
    private static Session? _session;
    private static CollectingEventSink _events = new();
    private static readonly Buffer _output = new();
    private static int _seen;
    private static bool _died;

    [JSExport]
    public static string Start(string worldJson, int seed)
    {
        var world = World.FromJson(worldJson);
        var issues = WorldValidator.Validate(world);
        if (WorldValidator.HasErrors(issues))
            return JsonSerializer.Serialize(new { type = "error", message = "world failed validation", issues = issues.Select(i => i.ToString()).ToArray() });
        _events = new CollectingEventSink();
        _engine = new GameEngine(world, _events, seed);
        _output.Drain();
        _seen = 0;
        _died = false;
        _engine.CharacterDied += _ => _died = true;
        _session = _engine.Enter("player", _output);
        return Turn("arrive", null);
    }

    [JSExport]
    public static string Send(string command)
    {
        if (_engine is null || _session is null) return JsonSerializer.Serialize(new { type = "error", message = "no game" });
        if (_died) return JsonSerializer.Serialize(new { type = "over", reason = "died" });
        _engine.HandleInput(_session, command);
        return Turn("turn", command);
    }

    private static string Turn(string type, string? command)
    {
        var fresh = _events.Events.Skip(_seen).Select(e => new
        {
            type = e.Type, actor = e.Actor, character = e.CharacterId, location = e.LocationId, payload = e.Payload,
        }).ToArray();
        _seen = _events.Events.Count;
        var s = _session!;
        return JsonSerializer.Serialize(new
        {
            type, command, text = _output.Drain(), events = fresh, alive = !_died,
            location = _died ? (int?)null : s.CurrentLocationId,
            hitPoints = _died ? 0 : s.Character.HitPoints,
            inventory = _died ? Array.Empty<string>() : s.Character.Inventory.Select(i => i.Name)
                .Concat(new[] { s.Character.Weapon?.Name, s.Character.Armor?.Name }.OfType<string>()).ToArray(),
        });
    }

    private sealed class Buffer : IOutputSink
    {
        private readonly List<string> _lines = [];
        public void Write(string message) => _lines.Add(message);
        public string[] Drain() { var d = _lines.ToArray(); _lines.Clear(); return d; }
    }
}
