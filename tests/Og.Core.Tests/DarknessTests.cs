using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>
/// Dark places without a light: the place and what is in it go unseen, a fall
/// may happen on entering, and blows are struck at a penalty.
/// </summary>
public class DarknessTests
{
    private static string Json(int fallChance = 0, int attackPenalty = 0) => $$"""
    {
      "spawnLocation": 1,
      "darkness": { "description": "It's pitch dark.", "creature": "Something breathes nearby.",
                    "fallChance": {{fallChance}}, "fallDamage": 3, "fallText": "You fall.",
                    "attackPenalty": {{attackPenalty}} },
      "items": [
        { "name": "lamp", "description": "A lit lamp.", "isLight": true },
        { "name": "pebble", "description": "A pebble." }
      ],
      "monsters": [
        { "name": "wolf", "description": "A grey wolf.", "hitPoints": 1, "armorClass": 1,
          "attackStrength": 0, "attackDice": 1 }
      ],
      "locations": [
        { "index": 1, "name": "Entrance", "description": "An entrance.", "connections": { "north": 2 },
          "ground": ["lamp"] },
        { "index": 2, "name": "Den", "description": "A den, bones everywhere.", "isDark": true,
          "monster": "wolf", "ground": ["pebble"], "connections": { "south": 1 } }
      ]
    }
    """;

    [Fact]
    public void InTheDarkThePlaceAndWhatIsInItAreUnseen()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json());

        engine.HandleInput(session, "north");

        Assert.Contains("It's pitch dark.", output.Text);
        Assert.Contains("Something breathes nearby.", output.Text);
        Assert.DoesNotContain("bones", output.Text);
        Assert.DoesNotContain("A grey wolf", output.Text);
        Assert.DoesNotContain("pebble", output.Text);
        Assert.Contains("Exits:", output.Text);

        output.Clear();
        engine.HandleInput(session, "examine wolf");
        Assert.Contains("too dark", output.Text);
    }

    [Fact]
    public void ALightShowsItAll()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json());

        engine.HandleInput(session, "take lamp");
        engine.HandleInput(session, "north");

        Assert.Contains("bones everywhere", output.Text);
        Assert.Contains("A grey wolf", output.Text);
        Assert.Contains("pebble", output.Text);
    }

    [Fact]
    public void ADarkPlaceCanTripYou()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json(fallChance: 100));

        engine.HandleInput(session, "north");

        var fell = Assert.Single(events.Events, e => e.Type == EventType.Fell);
        Assert.Contains("You fall.", output.Text);
        Assert.Equal(session.Character.MaxHitPoints - int.Parse(fell.Payload["damage"]), session.Character.HitPoints);
    }

    [Fact]
    public void WithALightThereIsNoFall()
    {
        var (engine, events, session, _) = WorldFixture.Start(Json(fallChance: 100));

        engine.HandleInput(session, "take lamp");
        engine.HandleInput(session, "north");

        Assert.DoesNotContain(events.Events, e => e.Type == EventType.Fell);
    }

    [Fact]
    public void AFallCanKill()
    {
        var (engine, events, session, _) = WorldFixture.Start(Json(fallChance: 100));
        session.Character.HitPoints = 1;

        engine.HandleInput(session, "north");

        Assert.True(session.Character.IsDead);
        Assert.Contains(events.Events, e => e.Type == EventType.Death && e.Payload["cause"] == "a fall in the dark");
    }

    [Fact]
    public void BlowsInTheDarkTakeThePenalty()
    {
        var (engine, events, session, _) = WorldFixture.Start(Json(attackPenalty: 100));

        engine.HandleInput(session, "north");
        for (var i = 0; i < 10; i++) engine.HandleInput(session, "attack wolf");

        Assert.All(events.Events.Where(e => e.Type == EventType.Combat && e.Payload.GetValueOrDefault("target") == "wolf"),
            e => Assert.Equal("miss", e.Payload["result"]));
    }

    [Fact]
    public void ADarkPlaceNeedsTheWorldToSayWhatDarknessIsLike()
    {
        var undark = Json().Replace("\"darkness\": {", "\"unused\": {");

        Assert.Contains(WorldValidator.Validate(World.FromJson(undark)), issue => issue.Message.Contains("dark"));
    }
}
