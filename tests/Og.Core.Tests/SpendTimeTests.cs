using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>The spendtime action costs time without moving.</summary>
public class SpendTimeTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "clock": { "moves": 2, "status": "Time for {0} more attempt{1}.", "expired": "Dusk falls." },
      "locations": [
        { "index": 1, "name": "Hall", "description": "A hall.",
          "interactions": [
            { "interactionId": "try", "triggers": ["try*"],
              "actions": [ { "action": "spendtime", "parameters": ["1"] },
                           { "action": "describe", "parameters": ["You try."] } ] }
          ] }
      ]
    }
    """;

    [Fact]
    public void EachTryCostsTimeAndSaysWhatIsLeft()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json, seed: 1);

        engine.HandleInput(session, "try the thing");
        Assert.Contains("Time for 1 more attempt.", output.Text);
        Assert.Contains("You try.", output.Text);
        Assert.True(output.Text.IndexOf("You try.") < output.Text.IndexOf("Time for 1"), "the time left comes after the answer");

        engine.HandleInput(session, "try again");
        Assert.Contains("Time for 0 more attempts.", output.Text);

        output.Clear();
        engine.HandleInput(session, "try once more");
        Assert.True(session.OutOfTime);
        Assert.Contains("Dusk falls.", output.Text);
        Assert.DoesNotContain("You try.", output.Text);
        Assert.Single(events.Events, e => e.Type == EventType.TimeRanOut);
    }
}
