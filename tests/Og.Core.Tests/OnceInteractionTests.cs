namespace Og.Core.Tests;

/// <summary>An interaction marked once fires only once for a character.</summary>
public class OnceInteractionTests
{
    [Fact]
    public void ItFiresOnlyOnce()
    {
        var (engine, _, session, output) = WorldFixture.Start();

        engine.HandleInput(session, "east");
        engine.HandleInput(session, "search the camp");
        output.Clear();
        engine.HandleInput(session, "search the camp");

        Assert.Single(session.Character.Inventory, item => item.Name == "shiny spoon");
        Assert.DoesNotContain("You turn over the ashes", output.Text);
    }
}
