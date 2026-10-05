using Og.Core.Definitions;
using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// Applies every command to the world. All input goes through <see cref="HandleInput"/>.
/// </summary>
public sealed class GameEngine
{
    private readonly Random _random;
    private readonly IEventSink _events;
    private readonly CommandRegistry _commands = new();

    // Set by a spendtime action, so the time left is said once the interaction has answered.
    private bool _timeSpent;

    public World World { get; }

    /// <summary>Raised when a character dies, so the host can close out its session.</summary>
    public event Action<Session>? CharacterDied;

    public GameEngine(World world, IEventSink events, int seed = 0)
    {
        World = world;
        _events = events;
        _random = seed == 0 ? new Random() : new Random(seed);
        DefaultCommands.Register(_commands);
    }

    public void Emit(GameEvent gameEvent) => _events.Emit(gameEvent);

    // ---- life and death -----------------------------------------------------

    /// <summary>Births a new character into the world at the spawn location.</summary>
    public Session Enter(string name, IOutputSink output)
    {
        var character = new Character(Guid.NewGuid().ToString("n")[..12], name)
        {
            CurrentLocationId = World.SpawnLocation
        };

        foreach (var item in World.StartingItems.Select(World.CreateItem).OfType<Item>())
        {
            character.Receive(item);
        }

        var session = new Session(character, output);

        Emit(GameEvent.For(session, EventType.CharacterBorn, ("name", name)));
        Emit(GameEvent.For(session, EventType.Arrived));

        session.Write(DescribeCurrentLocation(session));

        return session;
    }

    /// <summary>A living character leaving the world without dying.</summary>
    public void Depart(Session session) => Emit(GameEvent.For(session, EventType.Departed));

    /// <summary>
    /// The character is marked dead, drops everything it carried where it fell,
    /// and its session ends.
    /// </summary>
    public void Die(Session session, string cause)
    {
        var character = session.Character;
        var location = CurrentLocation(session);

        character.MarkDead(cause);

        var dropped = character.AllPossessions().ToList();

        if (location is not null)
        {
            location.Items.AddRange(dropped);
        }

        character.Inventory.Clear();
        character.Weapon = null;
        character.Armor = null;

        Emit(GameEvent.For(session, EventType.Death,
            ("cause", cause),
            ("name", character.Name),
            ("lifetime_seconds", ((int)(DateTimeOffset.UtcNow - character.BornAt).TotalSeconds).ToString()),
            ("dropped", string.Join(", ", dropped.Select(item => item.Name)))));

        session.Write($"\nYou are killed by {cause}.\n\nThat is the end of {character.Name}.");

        CharacterDied?.Invoke(session);
    }

    public Location? CurrentLocation(Session session) => World.FindLocation(session.CurrentLocationId);

    // ---- input ---------------------------------------------------------------

    public void HandleInput(Session session, string rawCommand)
    {
        if (string.IsNullOrWhiteSpace(rawCommand) || session.Over)
        {
            return;
        }

        var raw = rawCommand.Trim();
        var command = TriggerMatcher.Normalize(rawCommand);

        Emit(GameEvent.For(session, EventType.Command, ("text", raw)));

        var result = _commands.Execute(this, session, command, raw);

        if (result.Status == CommandStatus.Done)
        {
            return;
        }

        // A verb that came to nothing still carries on to the place the player is
        // standing in: the room may know what the engine does not.
        if (TryInteractions(session, command))
        {
            return;
        }

        // Nothing anywhere had an answer. Recorded rather than discarded, with
        // whatever the engine was going to say about it.
        Emit(GameEvent.For(session, EventType.UnparsedCommand,
            ("text", raw),
            ("kind", result.Status == CommandStatus.Futile ? "no effect" : "unrecognised"),
            ("refusal", result.Message ?? "")));

        session.Write(result.Message ?? "Nothing happens.");
    }

    /// <summary>
    /// Leaves the world if a key is held. The exit command and the escape action
    /// both come here, so leaving means the same thing whichever way it is reached.
    /// </summary>
    public bool Escape(Session session)
    {
        if ((World.ExitNeedsKey && !session.Character.HasKey) || session.OutOfTime || session.Ended)
        {
            return false;
        }

        Emit(GameEvent.For(session, EventType.Escaped));
        session.Write("You have escaped! Congratulations!");
        return true;
    }

    /// <summary>Offers a command to the room the player is in. True if the room answered.</summary>
    internal bool TryInteractions(Session session, string command)
    {
        var location = CurrentLocation(session);

        if (location is null)
        {
            return false;
        }

        // Snapshot: an action may enable or disable interactions mid-iteration.
        foreach (var interaction in location.Interactions.ToArray())
        {
            if (!interaction.IsEligible(World, session) || !interaction.MatchesTrigger(command))
            {
                continue;
            }

            Emit(GameEvent.For(session, EventType.InteractionFired,
                ("interaction", interaction.Id ?? "(anonymous)"),
                ("command", command)));

            _timeSpent = false;
            foreach (var action in interaction.Actions)
            {
                // An action that ended the session ends the interaction: nothing after it happens.
                if (session.OutOfTime || session.Ended)
                {
                    break;
                }

                Execute(session, action);
            }

            if (_timeSpent && !session.OutOfTime && RunningClock(session) is { } clock)
            {
                session.Write(ClockStatus(clock, session));
            }

            if (interaction.Once && interaction.Id is not null)
            {
                session.Character.FiredInteractions.Add(interaction.Id);
            }

            return true;
        }

        return false;
    }

    // ---- interaction actions --------------------------------------------------

    private void Execute(Session session, WorldAction action)
    {
        switch (action.Kind)
        {
            case ActionKind.Describe:
                session.Write(action.FirstParameter);
                break;

            case ActionKind.AddItems:
                foreach (var item in action.Parameters.Select(World.CreateItem).OfType<Item>())
                {
                    // A weapon or armour received into an empty slot is equipped, as
                    // when it is taken from the ground.
                    session.Write(session.Character.Receive(item)
                        ? $"You received {item.Name}, and {Character.Readied(item)}."
                        : $"You received {item.Name}.");

                    Emit(GameEvent.For(session, EventType.ItemPickedUp, ("item", item.Name)));
                }
                break;

            case ActionKind.RemoveItems:
                foreach (var name in action.Parameters)
                {
                    if (session.Character.FindItem(name) is { } item)
                    {
                        session.Character.Inventory.Remove(item);
                        Emit(GameEvent.For(session, EventType.ItemDropped, ("item", item.Name)));
                    }
                }
                break;

            case ActionKind.ChangeLocation:
                MoveTo(session, action.FirstParameterAsInt);
                break;

            case ActionKind.SetLocationDescription:
                if (World.FindLocation(action.FirstParameterAsInt) is { } target
                    && action.Parameters.Length > 1)
                {
                    target.Description = action.Parameters[1];
                }
                break;

            case ActionKind.EnableInteraction:
                SetInteractionDisabled(session, action, disabled: false);
                break;

            case ActionKind.DisableInteraction:
                SetInteractionDisabled(session, action, disabled: true);
                break;

            case ActionKind.AddCondition:
                ConditionSet(session, action.Scope).Add(action.FirstParameter);
                break;

            case ActionKind.RemoveCondition:
                ConditionSet(session, action.Scope).Remove(action.FirstParameter);
                break;

            case ActionKind.Escape:
                if (!Escape(session))
                {
                    session.Write(action.Parameters.Length > 0 ? action.FirstParameter : "It won't open without a key.");
                }
                break;

            case ActionKind.EndGame:
                session.Ended = true;
                Emit(GameEvent.For(session, EventType.GameEnded, ("reason", action.FirstParameter)));
                break;

            case ActionKind.SpendTime:
                // If time has run out the session ends and later actions are skipped.
                // Otherwise the time left is reported after the interaction's output.
                if (SpendTime(session, Math.Max(1, action.FirstParameterAsInt)))
                {
                    _timeSpent = true;
                }
                break;

            case ActionKind.PlaceItems:
                if (World.FindLocation(action.FirstParameterAsInt) is { } ground)
                {
                    ground.Items.AddRange(action.Parameters.Skip(1).Select(World.CreateItem).OfType<Item>());
                }
                break;

            case ActionKind.SetMonsterDescription:
                if (World.FindLocation(action.FirstParameterAsInt)?.Monster is { IsAlive: true } described
                    && action.Parameters.Length > 1)
                {
                    described.Description = action.Parameters[1];
                }
                break;

            case ActionKind.RemoveMonster:
                var place = action.Parameters.Length > 0 ? World.FindLocation(action.FirstParameterAsInt)
                                                         : CurrentLocation(session);
                if (place?.Monster is { IsAlive: true } leaving)
                {
                    place.Monster = null;
                    Emit(GameEvent.For(session, EventType.MonsterLeft, ("monster", leaving.Name)));
                }
                break;
        }
    }

    private HashSet<string> ConditionSet(Session session, Scope scope) =>
        scope == Scope.World ? World.Conditions : session.Character.Conditions;

    private void SetInteractionDisabled(Session session, WorldAction action, bool disabled)
    {
        var id = action.FirstParameter;

        if (action.Scope == Scope.Player)
        {
            if (disabled)
            {
                session.Character.DisabledInteractions.Add(id);
            }
            else
            {
                session.Character.DisabledInteractions.Remove(id);
            }

            return;
        }

        if (World.FindInteraction(id) is { } interaction)
        {
            interaction.DisabledForWorld = disabled;
        }
    }

    // ---- movement --------------------------------------------------------------

    public void MoveTo(Session session, int locationId)
    {
        var destination = World.FindLocation(locationId);

        if (destination is null)
        {
            Emit(GameEvent.For(session, EventType.BlockedMovement, ("target", locationId.ToString())));
            session.Write("You can't go that way.");
            return;
        }

        if (!SpendTime(session, 1))
        {
            return;
        }

        var from = session.CurrentLocationId;
        session.CurrentLocationId = destination.Id;

        Emit(GameEvent.For(session, EventType.Movement,
            ("from", from.ToString()), ("to", destination.Id.ToString())));

        session.Write(DescribeCurrentLocation(session));
        StumbleInTheDark(session);

        if (World.ExitOnArrival && destination.IsExit && session.Character.IsAlive)
        {
            Escape(session);
        }
    }

    /// <summary>
    /// Takes time off the world's clock: one move, or what an action costs. True
    /// if there was time for it; false, and the session over, if there was none.
    /// With no clock, there is always time.
    /// </summary>
    public bool SpendTime(Session session, int amount)
    {
        if (RunningClock(session) is not { } clock)
        {
            return true;
        }

        if (session.Moves >= clock.Moves)
        {
            session.OutOfTime = true;
            Emit(GameEvent.For(session, EventType.TimeRanOut, ("moves", session.Moves.ToString())));
            session.Write(clock.Expired);
            return false;
        }

        session.Moves += Math.Max(1, amount);
        return true;
    }

    /// <summary>The world's clock, unless there is none or its stopping condition has been set.</summary>
    private ClockDefinition? RunningClock(Session session) =>
        World.Clock is { } clock && (clock.StopsWhen is not { } stop || !World.HasCondition(session, stop)) ? clock : null;

    private static string ClockStatus(ClockDefinition clock, Session session)
    {
        var left = Math.Max(clock.Moves - session.Moves, 0);
        return string.Format(clock.Status, left, left == 1 ? "" : "s");
    }

    /// <summary>True where the player is in a dark place with no light.</summary>
    public bool InTheDark(Session session) =>
        World.Darkness is not null && CurrentLocation(session) is { IsDark: true } && !session.Character.HasLight;

    private void StumbleInTheDark(Session session)
    {
        if (!InTheDark(session) || World.Darkness is not { FallChance: > 0 } darkness)
        {
            return;
        }

        if (_random.Next(1, 101) > darkness.FallChance)
        {
            return;
        }

        var character = session.Character;
        var damage = _random.Next(1, Math.Max(1, darkness.FallDamage) + 1);
        character.HitPoints = Math.Max(0, character.HitPoints - damage);

        session.Write($"{darkness.FallText} HP: {character.HitPoints}/{character.MaxHitPoints}");
        Emit(GameEvent.For(session, EventType.Fell, ("damage", damage.ToString())));

        if (!character.IsAlive)
        {
            Die(session, "a fall in the dark");
        }
    }

    // ---- combat ------------------------------------------------------------------

    public void Attack(Session session)
    {
        var location = CurrentLocation(session);

        if (location?.Monster is not { } monster || !monster.IsAlive)
        {
            session.Write("There is nothing to attack here.");
            return;
        }

        var character = session.Character;
        var attackRoll = _random.Next(1, 21) + character.AttackStrength
                         - (InTheDark(session) ? World.Darkness!.AttackPenalty : 0);
        var damage = _random.Next(1, character.AttackDice + 1) + character.AttackStrength;

        if (monster.ReceiveAttack(attackRoll, damage))
        {
            session.Write("You hit!");
            Emit(GameEvent.For(session, EventType.Combat,
                ("target", monster.Name), ("result", "hit"), ("damage", damage.ToString())));

            if (!monster.IsAlive)
            {
                session.Write($"The {monster.Name} has died.");

                location.Items.AddRange(monster.Inventory);
                World.Slain.Add(monster.Name);
                Emit(GameEvent.For(session, EventType.MonsterSlain, ("monster", monster.Name)));
                location.Monster = null;

                if (location.Items.Count > 0)
                {
                    session.Write($"On the ground: {string.Join(", ", location.Items.Select(item => item.Name))}.");
                }

                return;
            }
        }
        else
        {
            session.Write("You missed!");
            Emit(GameEvent.For(session, EventType.Combat, ("target", monster.Name), ("result", "miss")));
        }

        var monsterRoll = _random.Next(1, 21) + monster.AttackStrength;
        var monsterDamage = _random.Next(1, monster.AttackDice + 1) + monster.AttackStrength;

        if (character.ReceiveAttack(monsterRoll, monsterDamage))
        {
            session.Write($"You were hit! HP: {character.HitPoints}/{character.MaxHitPoints}");

            if (!character.IsAlive)
            {
                Die(session, $"a {monster.Name}");
            }
        }
        else
        {
            session.Write($"The {monster.Name} missed!");
        }
    }

    // ---- description ----------------------------------------------------------------

    public string DescribeCurrentLocation(Session session)
    {
        var location = CurrentLocation(session);

        if (location is null)
        {
            return "You are nowhere.";
        }

        var dark = InTheDark(session);
        var parts = new List<string> { dark ? location.DarkDescription ?? World.Darkness!.Description : location.Description };

        if (location.Monster is { IsAlive: true } monster)
        {
            parts.Add(dark ? World.Darkness!.Creature : monster.Description);
        }

        if (location.Items.Count > 0 && !dark)
        {
            parts.Add($"On the ground: {string.Join(", ", location.Items.Select(item => item.Name))}.");
        }

        var directions = location.OpenDirections()
            .Select(direction => direction.ToString().ToLowerInvariant())
            .ToList();

        if (directions.Count > 0)
        {
            // Exits are listed plainly so they are not read as objects in the room.
            parts.Add($"Exits: {string.Join(", ", directions)}.");
        }

        if (location.IsExit)
        {
            parts.Add("You've found the exit!");
        }

        if (RunningClock(session) is { } clock)
        {
            parts.Add(ClockStatus(clock, session));
        }

        return string.Join("\n\n", parts);
    }
}
