using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>The exit command, with and without ExitNeedsKey.</summary>
public class ExitTests
{
    private static string Json(bool exitNeedsKey) => $$"""
    {
      "spawnLocation": 1, "exitNeedsKey": {{(exitNeedsKey ? "true" : "false")}},
      "locations": [
        { "index": 1, "name": "Tunnel", "description": "A tunnel.", "connections": { "north": 2 } },
        { "index": 2, "name": "Daylight", "description": "Daylight.", "isExit": true, "connections": { "south": 1 } }
      ]
    }
    """;

    [Fact]
    public void AWayOutCanNeedNoKey()
    {
        var (engine, events, session, _) = WorldFixture.Start(Json(exitNeedsKey: false));

        engine.HandleInput(session, "north");
        engine.HandleInput(session, "exit");

        Assert.Single(events.Events, e => e.Type == EventType.Escaped);
    }

    [Fact]
    public void AWayOutCanStillNeedAKey()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json(exitNeedsKey: true));

        engine.HandleInput(session, "north");
        engine.HandleInput(session, "exit");

        Assert.DoesNotContain(events.Events, e => e.Type == EventType.Escaped);
        Assert.Contains("You don't have a key.", output.Text);
    }

    [Fact]
    public void ExitingAnywhereElseIsRefused()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json(exitNeedsKey: false));

        engine.HandleInput(session, "exit");

        Assert.DoesNotContain(events.Events, e => e.Type == EventType.Escaped);
        Assert.Contains("not the exit", output.Text);
    }
}
