using Og.Core.Runtime;

namespace Og.Core.Tests;

public class EventLogTests
{
    [Fact]
    public void UnrecognizedCommandsAreLoggedAsRequests()
    {
        var (engine, events) = WorldFixture.NewEngine();
        var (alice, _) = WorldFixture.Enter(engine, "Alice");

        engine.HandleInput(alice, "light a fire");

        Assert.Contains(events.Events, e =>
            e.Type == EventType.UnparsedCommand && e.Payload["text"] == "light a fire");
    }

    [Fact]
    public void PrayerIsRecordedButUnanswered()
    {
        var (engine, events) = WorldFixture.NewEngine();
        var (alice, aliceOut) = WorldFixture.Enter(engine, "Alice");

        engine.HandleInput(alice, "pray for a sword");

        var prayer = Assert.Single(events.Events, e => e.Type == EventType.Speech && e.Payload["mode"] == "pray");
        Assert.Equal("for a sword", prayer.Payload["text"]);
        Assert.Equal("You pray.", aliceOut.Text);
    }

    [Fact]
    public void EveryCommandIsLogged()
    {
        var (engine, events) = WorldFixture.NewEngine();
        var (alice, _) = WorldFixture.Enter(engine, "Alice");

        engine.HandleInput(alice, "look");
        engine.HandleInput(alice, "north");

        Assert.Equal(2, events.Events.Count(e => e.Type == EventType.Command));
        Assert.Contains(events.Events, e => e.Type == EventType.Movement);
    }

    [Fact]
    public void SpeechKeepsPunctuationAndCapitals()
    {
        var (engine, events) = WorldFixture.NewEngine();
        var (alice, aliceOut) = WorldFixture.Enter(engine, "Alice");

        engine.HandleInput(alice, "say Hello, friend. How are you?");

        Assert.Contains("You say: Hello, friend. How are you?", aliceOut.Text);

        var speech = Assert.Single(events.Events, e => e.Type == EventType.Speech);
        Assert.Equal("Hello, friend. How are you?", speech.Payload["text"]);
    }

    [Fact]
    public void UnparsedCommandsKeepWhatWasActuallyTyped()
    {
        var (engine, events) = WorldFixture.NewEngine();
        var (alice, _) = WorldFixture.Enter(engine, "Alice");

        engine.HandleInput(alice, "Light the brazier, please!");

        var request = Assert.Single(events.Events, e => e.Type == EventType.UnparsedCommand);
        Assert.Equal("Light the brazier, please!", request.Payload["text"]);
    }

    [Fact]
    public void VerbMatchingIsStillCaseAndPunctuationInsensitive()
    {
        var (engine, _) = WorldFixture.NewEngine();
        var (alice, aliceOut) = WorldFixture.Enter(engine, "Alice");

        engine.HandleInput(alice, "  LOOK!  ");

        Assert.Contains("small clearing", aliceOut.Text);
    }
}
