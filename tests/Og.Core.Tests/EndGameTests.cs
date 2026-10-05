using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>The endgame action ends the session without an escape, and nothing happens after.</summary>
public class EndGameTests
{
    private const string Json = """
    {
      "spawnLocation": 1, "exitNeedsKey": false,
      "items": [ { "name": "token", "description": "A token." } ],
      "startingItems": ["token"],
      "locations": [ { "index": 1, "name": "Hall", "description": "A hall.", "isExit": true,
        "interactions": [ { "interactionId": "give-away", "triggers": ["give*&*token*"],
          "actions": [ { "action": "removeitems", "parameters": ["token"] },
                       { "action": "describe", "parameters": ["The door closes behind them."] },
                       { "action": "endgame", "parameters": ["stayed"] },
                       { "action": "describe", "parameters": ["NEVER SAID"] } ] } ] } ]
    }
    """;

    [Fact]
    public void TheGameEndsWithItsReasonAndGoesNoFurther()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json, seed: 1);

        engine.HandleInput(session, "give the token to the old man");
        Assert.True(session.Ended);
        Assert.Contains("The door closes behind them.", output.Text);
        Assert.DoesNotContain("NEVER SAID", output.Text);
        Assert.Equal("stayed", Assert.Single(events.Events, e => e.Type == EventType.GameEnded).Payload["reason"]);

        engine.HandleInput(session, "exit");
        Assert.DoesNotContain(events.Events, e => e.Type == EventType.Escaped);
    }
}
