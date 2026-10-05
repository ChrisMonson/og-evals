using Og.Core.Runtime;

namespace Og.Core.Tests;

public class ExitOnArrivalTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void ReachingTheExitIsLeavingWhenTheWorldSaysSo(bool onArrival, int escapes)
    {
        var json = $$"""
        {
          "spawnLocation": 1, "exitNeedsKey": false, "exitOnArrival": {{(onArrival ? "true" : "false")}},
          "locations": [
            { "index": 1, "name": "Tunnel", "description": "A tunnel.", "connections": { "north": 2 } },
            { "index": 2, "name": "Daylight", "description": "Daylight.", "isExit": true, "connections": { "south": 1 } }
          ]
        }
        """;
        var (engine, events, session, _) = WorldFixture.Start(json, seed: 1);

        engine.HandleInput(session, "north");

        Assert.Equal(escapes, events.Events.Count(e => e.Type == EventType.Escaped));
    }
}
