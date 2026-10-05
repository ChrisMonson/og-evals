using Og.Core.Model;

namespace Og.Core.Runtime;

public sealed record ValidationIssue(string Severity, string Message)
{
    public const string Error = "error";
    public const string Warning = "warning";

    public override string ToString() => $"[{Severity}] {Message}";
}

/// <summary>Structural checks over a loaded world.</summary>
public static class WorldValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(World world)
    {
        var issues = new List<ValidationIssue>();
        var ids = world.Locations.Select(location => location.Id).ToHashSet();

        if (ids.Count == 0)
        {
            issues.Add(new ValidationIssue(ValidationIssue.Error, "World has no locations."));
            return issues;
        }

        if (!ids.Contains(world.SpawnLocation))
        {
            issues.Add(new ValidationIssue(ValidationIssue.Error,
                $"Spawn location {world.SpawnLocation} does not exist."));
        }

        if (world.Locations.Any(location => location.IsDark) && world.Darkness is null)
        {
            issues.Add(new ValidationIssue(ValidationIssue.Error,
                "A place is dark, but the world does not say what darkness is like."));
        }

        if (world.Darkness is { FallChance: < 0 or > 100 })
        {
            issues.Add(new ValidationIssue(ValidationIssue.Error, "A fall chance is a percentage: 0 to 100."));
        }

        foreach (var action in world.Locations.SelectMany(location => location.Interactions)
                     .SelectMany(interaction => interaction.Actions)
                     .Where(action => action.Kind == ActionKind.PlaceItems))
        {
            if (world.FindLocation(action.FirstParameterAsInt) is null)
            {
                issues.Add(new ValidationIssue(ValidationIssue.Error,
                    $"placeitems names location '{action.FirstParameter}', which does not exist."));
            }

            foreach (var name in action.Parameters.Skip(1).Where(name => world.FindItemDefinition(name) is null))
            {
                issues.Add(new ValidationIssue(ValidationIssue.Error,
                    $"placeitems names '{name}', but no such item exists."));
            }
        }

        if (world.Clock is null && world.Locations.SelectMany(location => location.Interactions)
                .SelectMany(interaction => interaction.Actions).Any(action => action.Kind == ActionKind.SpendTime))
        {
            issues.Add(new ValidationIssue(ValidationIssue.Warning,
                "spendtime is used, but the world has no clock: it will cost nothing."));
        }

        if (world.Clock is { Moves: < 1 })
        {
            issues.Add(new ValidationIssue(ValidationIssue.Error, "A clock needs at least one move."));
        }

        foreach (var location in world.Locations)
        {
            foreach (var direction in Enum.GetValues<Direction>())
            {
                var target = location.Exit(direction);

                if (target != 0 && !ids.Contains(target))
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {location.Id} exits {direction} to {target}, which does not exist."));
                }
            }

            foreach (var name in world.GroundOf(location.Id))
            {
                if (world.FindItemDefinition(name) is null)
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {location.Id} puts unknown item '{name}' on the ground."));
                }
            }

            foreach (var interaction in location.Interactions)
            {
                // A carrying: condition naming an item that does not exist can never
                // hold, and nothing else would ever say so.
                foreach (var condition in interaction.If.Concat(interaction.IfNot))
                {
                    if (condition.StartsWith(World.CarryingPrefix, StringComparison.OrdinalIgnoreCase)
                        && world.FindItemDefinition(condition[World.CarryingPrefix.Length..].Trim()) is null)
                    {
                        issues.Add(new ValidationIssue(ValidationIssue.Error,
                            $"Location {location.Id} checks '{condition}', but no such item exists."));
                    }

                    if (condition.StartsWith(World.SlainPrefix, StringComparison.OrdinalIgnoreCase)
                        && !world.HasMonsterDefinition(condition[World.SlainPrefix.Length..]))
                    {
                        issues.Add(new ValidationIssue(ValidationIssue.Error,
                            $"Location {location.Id} checks '{condition}', but no such creature exists."));
                    }
                }

                if (interaction.Triggers.Length == 0)
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {location.Id} has an interaction with no triggers."));
                }

                foreach (var group in interaction.Triggers.SelectMany(WordGroups.Unresolved).Distinct())
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {location.Id} has a trigger naming '{{{group}}}', but no such word group exists."));
                }

                foreach (var action in interaction.Actions)
                {
                    ValidateAction(world, ids, location.Id, action, issues);
                }
            }
        }

        ValidateReachability(world, issues);
        return issues;
    }

    private static void ValidateAction(
        World world,
        HashSet<int> locationIds,
        int locationId,
        WorldAction action,
        List<ValidationIssue> issues)
    {
        // An unknown action would be silently ignored at runtime.
        if (!ActionKinds.All.Contains(action.Kind))
        {
            issues.Add(new ValidationIssue(ValidationIssue.Error,
                $"Location {locationId} uses '{action.AsWritten}', which is not something an "
                + $"interaction can do. It would be read and then ignored. "
                + $"What there is: {string.Join(", ", ActionKinds.All.Order())}."));
            return;
        }

        switch (action.Kind)
        {
            case ActionKind.AddItems:
            case ActionKind.RemoveItems:
                foreach (var name in action.Parameters)
                {
                    if (world.FindItemDefinition(name) is null)
                    {
                        issues.Add(new ValidationIssue(ValidationIssue.Error,
                            $"Location {locationId} references unknown item '{name}'."));
                    }
                }
                break;

            case ActionKind.ChangeLocation:
            case ActionKind.SetLocationDescription:
            case ActionKind.SetMonsterDescription:
                if (!locationIds.Contains(action.FirstParameterAsInt))
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {locationId} targets location {action.FirstParameterAsInt}, which does not exist."));
                }
                break;

            case ActionKind.RemoveMonster:
                if (action.Parameters.Length > 0 && !locationIds.Contains(action.FirstParameterAsInt))
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {locationId} sends away the monster of location {action.FirstParameterAsInt}, which does not exist."));
                }
                break;

            case ActionKind.EnableInteraction:
            case ActionKind.DisableInteraction:
                if (action.Scope == Scope.World && world.FindInteraction(action.FirstParameter) is null)
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {locationId} toggles unknown interaction '{action.FirstParameter}'."));
                }
                break;
        }
    }

    private static void ValidateReachability(World world, List<ValidationIssue> issues)
    {
        var reached = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(world.SpawnLocation);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();

            if (!reached.Add(id) || world.FindLocation(id) is not { } location)
            {
                continue;
            }

            foreach (var direction in Enum.GetValues<Direction>())
            {
                if (location.Exit(direction) is var target and > 0)
                {
                    queue.Enqueue(target);
                }
            }

            // changelocation targets count as connections.
            foreach (var action in location.Interactions.SelectMany(interaction => interaction.Actions))
            {
                if (action.Kind == ActionKind.ChangeLocation)
                {
                    queue.Enqueue(action.FirstParameterAsInt);
                }
            }
        }

        foreach (var location in world.Locations.Where(candidate => !reached.Contains(candidate.Id)))
        {
            issues.Add(new ValidationIssue(ValidationIssue.Warning,
                $"Location {location.Id} ('{location.Name}') is unreachable from spawn."));
        }

        if (!world.Locations.Any(location => location.IsExit && reached.Contains(location.Id)))
        {
            issues.Add(new ValidationIssue(ValidationIssue.Warning, "No reachable exit in this world."));
        }
    }

    public static bool HasErrors(IEnumerable<ValidationIssue> issues) =>
        issues.Any(issue => issue.Severity == ValidationIssue.Error);
}
