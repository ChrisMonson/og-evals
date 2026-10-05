using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>A small world owned by the tests, so engine tests do not depend on any scenario.</summary>
public static class WorldFixture
{
    public const string Json = """
    {
      "name": "fixture",
      "spawnLocation": 1,
      "startingItems": ["dagger"],
      "items": [
        { "name": "dagger", "description": "A short blade.", "kind": "weapon", "attackDice": 4 },
        { "name": "shiny spoon", "description": "A spoon, and shiny." }
      ],
      "monsters": [
        { "name": "goblin", "description": "A goblin crouches here.", "hitPoints": 7,
          "armorClass": 12, "attackStrength": 1, "attackDice": 6 }
      ],
      "locations": [
        {
          "index": 1,
          "name": "The Clearing",
          "description": "You stand in a small clearing. There are ways north and east.",
          "connections": { "north": 2, "east": 3 }
        },
        {
          "index": 2,
          "name": "The North Room",
          "description": "A room north of the clearing.",
          "connections": { "south": 1 },
          "monster": "goblin"
        },
        {
          "index": 3,
          "name": "The Camp",
          "description": "A cold camp east of the clearing.",
          "connections": { "west": 1 },
          "interactions": [
            {
              "interactionId": "camp-search",
              "scope": "Player",
              "once": true,
              "triggers": ["*search the camp*"],
              "actions": [
                { "action": "describe", "parameters": ["You turn over the ashes and find something."] },
                { "action": "addItems", "parameters": ["shiny spoon"] }
              ]
            }
          ]
        }
      ]
    }
    """;

    public static World Load() => World.FromJson(Json);

    public static (GameEngine Engine, CollectingEventSink Events) NewEngine(int seed = 1234)
    {
        var events = new CollectingEventSink();
        return (new GameEngine(Load(), events, seed), events);
    }

    /// <summary>A new character in the engine, with the arrival text already cleared.</summary>
    public static (Session Session, CollectingOutputSink Output) Enter(GameEngine engine, string name = "Ada")
    {
        var output = new CollectingOutputSink();
        var session = engine.Enter(name, output);
        output.Clear();
        return (session, output);
    }

    /// <summary>An engine on the given world with one character in it, arrival text cleared.</summary>
    public static (GameEngine Engine, CollectingEventSink Events, Session Session, CollectingOutputSink Output) Start(
        string json = Json, int seed = 1234)
    {
        var events = new CollectingEventSink();
        var engine = new GameEngine(World.FromJson(json), events, seed);
        var (session, output) = Enter(engine);
        return (engine, events, session, output);
    }

    /// <summary>Kills a character deterministically, without relying on dice.</summary>
    public static void Kill(GameEngine engine, Session session, string cause = "the test")
    {
        session.Character.HitPoints = 0;
        engine.Die(session, cause);
    }

    public static bool IsValid(string json) =>
        !WorldValidator.HasErrors(WorldValidator.Validate(World.FromJson(json)));
}
