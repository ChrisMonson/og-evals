using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>"attack X with Y" names X as the target.</summary>
public class AttackWithTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "monsters": [ { "name": "wolf", "description": "A wolf.", "hitPoints": 99, "armorClass": 1, "attackStrength": 0, "attackDice": 1 } ],
      "locations": [ { "index": 1, "name": "Hall", "description": "A hall.", "monster": "wolf" } ]
    }
    """;

    [Theory]
    [InlineData("attack wolf with torch")]
    [InlineData("attack the wolf using my fists")]
    public void WhatTheBlowIsStruckWithIsNotPartOfTheName(string command)
    {
        var (engine, events, session, _) = WorldFixture.Start(Json, seed: 1);

        engine.HandleInput(session, command);

        Assert.Contains(events.Events, e => e.Type == EventType.Combat && e.Payload.GetValueOrDefault("target") == "wolf");
    }
}
