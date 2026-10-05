using Og.Core.Definitions;
using Og.Core.Model;

namespace Og.Core.Runtime;

public sealed class Interaction
{
    public string? Id { get; }
    public string[] Triggers { get; }
    public WorldAction[] Actions { get; }
    public string[] If { get; }
    public string[] IfNot { get; }
    public Scope Scope { get; }
    public bool Once { get; }

    /// <summary>World-scoped disabled state. Player-scoped state lives on the Character.</summary>
    public bool DisabledForWorld { get; set; }

    public Interaction(InteractionDefinition definition, IReadOnlyDictionary<string, string[]> words)
    {
        Id = definition.InteractionId;
        Triggers = WordGroups.Expand(definition.Triggers, words);
        If = definition.If;
        IfNot = definition.IfNot;
        Scope = definition.Scope;
        Once = definition.Once;
        DisabledForWorld = definition.Disabled;
        Actions = [.. definition.Actions.Select(action => new WorldAction(action))];
    }

    public bool IsDisabledFor(Session session) =>
        Scope == Scope.World
            ? DisabledForWorld
            : session.Character.DisabledInteractions.Contains(Id ?? "");

    /// <summary>
    /// Whether this interaction is currently eligible for the given player.
    /// Conditions are checked against the union of world and player flags.
    /// </summary>
    public bool IsEligible(World world, Session session)
    {
        if (IsDisabledFor(session))
        {
            return false;
        }

        if (Once && Id is not null && session.Character.FiredInteractions.Contains(Id))
        {
            return false;
        }

        foreach (var condition in If)
        {
            if (!world.HasCondition(session, condition))
            {
                return false;
            }
        }

        foreach (var condition in IfNot)
        {
            if (world.HasCondition(session, condition))
            {
                return false;
            }
        }

        return true;
    }

    public bool MatchesTrigger(string command) =>
        Triggers.Any(trigger => TriggerMatcher.Matches(trigger, command));
}
