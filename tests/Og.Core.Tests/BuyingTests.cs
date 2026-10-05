using Og.Core.Runtime;

namespace Og.Core.Tests;

/// <summary>
/// Buying: money is anything of the currency kind, the place decides what it
/// sells, and every attempt is recorded whether or not anything was sold.
/// </summary>
public class BuyingTests
{
    private const string Json = """
    {
      "name": "market",
      "spawnLocation": 1,
      "items": [
        { "name": "purse of gold", "description": "A purse.", "kind": "currency" },
        { "name": "bread", "description": "A loaf." }
      ],
      "locations": [
        { "index": 1, "name": "Road", "description": "A road.", "connections": { "east": 2 }, "ground": ["purse of gold"] },
        { "index": 2, "name": "Stall", "description": "A bread stall.", "connections": { "west": 1 },
          "interactions": [
            { "interactionId": "sell-bread", "triggers": ["buy bread*", "buy a loaf*", "buy the bread*"],
              "if": ["carrying:purse of gold"],
              "actions": [
                { "action": "additems", "parameters": ["bread"] },
                { "action": "describe", "parameters": ["The baker hands you a loaf."] }
              ] }
          ] }
      ]
    }
    """;

    private static GameEvent Bought(CollectingEventSink events) =>
        Assert.Single(events.Events, e => e.Type == EventType.TriedBuying);

    [Fact]
    public void WithoutMoneyNothingCanBeBought()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "buy a horse");

        Assert.Contains("nothing to pay with", output.Text);
        Assert.Equal("no", Bought(events).Payload["can_pay"]);
        Assert.Equal("horse", Bought(events).Payload["thing"]);
    }

    [Fact]
    public void WithMoneyButNoSellerItSaysSo()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "take purse of gold");
        output.Clear();
        engine.HandleInput(session, "buy a horse with the gold");

        Assert.Contains("Nobody here is selling horse.", output.Text);
        Assert.Equal("yes", Bought(events).Payload["can_pay"]);
        Assert.Equal("no", Bought(events).Payload["sold"]);
    }

    [Fact]
    public void APlaceThatSellsItAnswers()
    {
        var (engine, events, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "take purse of gold");
        engine.HandleInput(session, "east");
        output.Clear();
        engine.HandleInput(session, "buy bread");

        Assert.Contains("hands you a loaf", output.Text);
        Assert.NotNull(session.Character.FindItem("bread"));
        Assert.Equal("yes", Bought(events).Payload["sold"]);
    }

    [Fact]
    public void BuyingNothingAsksWhat()
    {
        var (engine, _, session, output) = WorldFixture.Start(Json);

        engine.HandleInput(session, "buy");

        Assert.Contains("Buy what?", output.Text);
    }
}
