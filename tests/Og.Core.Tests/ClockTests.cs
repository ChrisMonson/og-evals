using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>
/// The clock: a world can limit how many moves are made. Every place says how
/// much is left, and trying to move with none left ends the session, including
/// a move an interaction makes.
/// </summary>
public class ClockTests
{
    private const string Json = """
    {
      "spawnLocation": 1,
      "items": [ { "name": "key", "description": "An iron key.", "isKey": true } ],
      "clock": { "moves": 2, "status": "Daylight left: {0} field{1}.", "expired": "The sun sets. The gate is shut." },
      "locations": [
        { "index": 1, "name": "Yard", "description": "A yard.", "connections": { "east": 2 }, "ground": ["key"],
          "interactions": [
            { "interactionId": "nightfall", "triggers": ["wait"],
              "actions": [ { "action": "addcondition", "parameters": ["dark"] } ] },
            { "interactionId": "run-to-gate", "triggers": ["*gate*"],
              "actions": [
                { "action": "changelocation", "parameters": ["3"] },
                { "action": "escape", "parameters": ["The gate stays shut."] }
              ] }
          ] },
        { "index": 2, "name": "Lane", "description": "A lane.", "connections": { "west": 1, "east": 3 } },
        { "index": 3, "name": "Gate", "description": "A gate.", "connections": { "west": 2 }, "isExit": true }
      ]
    }
    """;

    [Fact]
    public void EveryPlaceSaysHowMuchIsLeft()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "look");
        Assert.Contains("Daylight left: 2 fields.", output.Text);
        output.Clear();

        engine.HandleInput(session, "east");

        Assert.Contains("Daylight left: 1 field.", output.Text);
    }

    [Fact]
    public void OnlyMovingSpendsIt()
    {
        var (engine, _, session, _) = WorldFixture.Start(Json);

        engine.HandleInput(session, "look");
        engine.HandleInput(session, "take key");
        engine.HandleInput(session, "dance a jig");
        engine.HandleInput(session, "north");

        Assert.Equal(0, session.Moves);
    }

    [Fact]
    public void TheLastMoveStillArrives()
    {
        var (engine, events, session, _) = WorldFixture.Start(Json);

        engine.HandleInput(session, "take key");
        engine.HandleInput(session, "east");
        engine.HandleInput(session, "east");
        engine.HandleInput(session, "exit");

        Assert.Single(events.Events, e => e.Type == EventType.Escaped);
        Assert.DoesNotContain(events.Events, e => e.Type == EventType.TimeRanOut);
    }

    [Fact]
    public void MovingWithNoneLeftEndsTheSession()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "east");
        engine.HandleInput(session, "west");
        output.Clear();
        engine.HandleInput(session, "east");

        Assert.True(session.OutOfTime);
        Assert.Equal(1, session.CurrentLocationId);
        Assert.Contains("The sun sets.", output.Text);
        Assert.Single(events.Events, e => e.Type == EventType.TimeRanOut);

        output.Clear();
        engine.HandleInput(session, "look");
        Assert.Equal("", output.Text);
    }

    [Fact]
    public void AnInteractionsMoveSpendsItTooAndStopsWhatFollows()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "take key");
        engine.HandleInput(session, "east");
        engine.HandleInput(session, "west");
        output.Clear();
        engine.HandleInput(session, "run to the gate");

        Assert.True(session.OutOfTime);
        Assert.DoesNotContain(events.Events, e => e.Type == EventType.Escaped);
        Assert.DoesNotContain("stays shut", output.Text);
    }

    [Fact]
    public void AConditionCanStopTheClock()
    {
        var json = Json.Replace("\"expired\": \"The sun sets. The gate is shut.\"",
                                "\"expired\": \"The sun sets. The gate is shut.\", \"stopsWhen\": \"dark\"");
        var (engine, _, session, output) = WorldFixture.Start(json);

        engine.HandleInput(session, "wait");
        output.Clear();
        engine.HandleInput(session, "east");
        engine.HandleInput(session, "west");
        engine.HandleInput(session, "east");

        Assert.Equal(0, session.Moves);
        Assert.False(session.OutOfTime);
        Assert.DoesNotContain("Daylight left", output.Text);
    }

    [Fact]
    public void AWorldWithoutAClockIsUnchanged()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json.Replace(
            "\"clock\": { \"moves\": 2, \"status\": \"Daylight left: {0} field{1}.\", \"expired\": \"The sun sets. The gate is shut.\" },", ""));

        for (var i = 0; i < 5; i++)
        {
            engine.HandleInput(session, "east");
            engine.HandleInput(session, "west");
        }

        Assert.False(session.OutOfTime);
        Assert.DoesNotContain("Daylight", output.Text);
    }

    [Fact]
    public void AClockWithNoMovesIsAnError()
    {
        var world = World.FromJson(Json.Replace("\"moves\": 2", "\"moves\": 0"));

        Assert.Contains(WorldValidator.Validate(world), issue => issue.Message.Contains("clock"));
    }
}
