using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>
/// Examining looks at what is carried, then the creature present, then items on
/// the ground. Anything else goes on to the room.
/// </summary>
public class ExamineTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "items": [ { "name": "lantern", "description": "A brass lantern, cold." } ],
      "monsters": [
        { "name": "guard", "description": "A guard turning a key on a string.", "hitPoints": 6,
          "armorClass": 8, "attackStrength": 0, "attackDice": 2 }
      ],
      "locations": [
        { "index": 1, "name": "Yard", "description": "A yard.", "connections": { "east": 2 }, "monster": "guard",
          "interactions": [
            { "interactionId": "carving", "triggers": ["*carving*"],
              "actions": [{ "action": "describe", "parameters": ["Someone carved a fish into the wall."] }] }
          ] },
        { "index": 2, "name": "Field", "description": "A field.", "connections": { "west": 1 }, "ground": ["lantern"] }
      ]
    }
    """;

    [Fact]
    public void ExaminingReachesTheCreatureInTheRoom()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "look at the guard");

        Assert.Contains("turning a key on a string", output.Text);
    }

    [Fact]
    public void ExaminingReachesSomethingOnTheGround()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);
        engine.HandleInput(session, "east");
        output.Clear();

        engine.HandleInput(session, "examine lantern");

        Assert.Contains("brass lantern", output.Text);
    }

    [Fact]
    public void ExaminingAFeatureOfTheRoomGoesOnToTheRoom()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "look at the carving");

        Assert.Contains("carved a fish", output.Text);
        Assert.Equal("carving", Assert.Single(events.Events, e => e.Type == EventType.InteractionFired).Payload["interaction"]);
    }
}
