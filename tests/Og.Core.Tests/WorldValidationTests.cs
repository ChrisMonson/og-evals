using Og.Core.Runtime;

namespace Og.Core.Tests;

public class WorldValidationTests
{
    [Fact]
    public void FixtureWorldHasNoErrors()
    {
        var issues = WorldValidator.Validate(WorldFixture.Load());
        Assert.DoesNotContain(issues, issue => issue.Severity == ValidationIssue.Error);
    }

    [Fact]
    public void DanglingConnectionIsAnError()
    {
        var world = World.FromJson("""
        {
          "spawnLocation": 1,
          "locations": [
            { "index": 1, "name": "A", "connections": { "north": 99 } }
          ]
        }
        """);

        var issues = WorldValidator.Validate(world);
        Assert.Contains(issues, issue => issue.Severity == ValidationIssue.Error && issue.Message.Contains("99"));
    }

    [Fact]
    public void UnknownItemReferenceIsAnError()
    {
        var world = World.FromJson("""
        {
          "spawnLocation": 1,
          "locations": [
            {
              "index": 1, "name": "A",
              "interactions": [
                { "triggers": ["*take*"], "actions": [ { "action": "addItems", "parameters": ["sword of nothing"] } ] }
              ]
            }
          ]
        }
        """);

        var issues = WorldValidator.Validate(world);
        Assert.Contains(issues, issue => issue.Message.Contains("sword of nothing"));
    }

    [Fact]
    public void UnreachableLocationIsAWarning()
    {
        var world = World.FromJson("""
        {
          "spawnLocation": 1,
          "locations": [
            { "index": 1, "name": "A", "isExit": true },
            { "index": 2, "name": "Orphan" }
          ]
        }
        """);

        var issues = WorldValidator.Validate(world);
        Assert.Contains(issues, issue =>
            issue.Severity == ValidationIssue.Warning && issue.Message.Contains("Orphan"));
    }

    private static World WithAction(string action) => World.FromJson($$"""
    {
      "spawnLocation": 1,
      "locations": [
        {
          "index": 1, "name": "A", "isExit": true,
          "interactions": [
            { "interactionId": "x", "triggers": ["*x*"],
              "actions": [ { "action": "{{action}}", "parameters": ["something"] } ] }
          ]
        }
      ]
    }
    """);

    [Fact]
    public void AnActionNothingImplementsIsRejected()
    {
        var issues = WorldValidator.Validate(WithAction("setFlag"));

        Assert.True(WorldValidator.HasErrors(issues));
        Assert.Contains(issues, issue => issue.Message.Contains("setFlag"));
    }

    [Fact]
    public void AndTheRejectionSaysWhatThereIsInstead()
    {
        var issues = WorldValidator.Validate(WithAction("setFlag"));
        var complaint = issues.First(issue => issue.Message.Contains("setFlag")).Message;

        Assert.Contains("addcondition", complaint);
        Assert.Contains("describe", complaint);
    }

    [Fact]
    public void AKnownActionIsAccepted()
    {
        var issues = WorldValidator.Validate(WithAction("addCondition"));

        Assert.False(WorldValidator.HasErrors(issues));
    }
}
