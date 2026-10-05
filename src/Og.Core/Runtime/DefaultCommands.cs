using System.Text.RegularExpressions;
using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// The verbs the engine knows. A handler returns Done only when something
/// actually happened; when it recognises the words but can do nothing with them
/// it returns Futile, and the command carries on to the place the player is in.
/// </summary>
public static class DefaultCommands
{
    public static void Register(CommandRegistry registry)
    {
        RegisterMovement(registry);
        RegisterObservation(registry);
        RegisterInventory(registry);
        RegisterGiving(registry);
        RegisterBuying(registry);
        RegisterCombat(registry);
        RegisterLeaving(registry);
        RegisterSpeech(registry);
    }

    private static void RegisterMovement(CommandRegistry registry)
    {
        Move(Direction.North, "go north", "north", "n");
        Move(Direction.East, "go east", "east", "e");
        Move(Direction.South, "go south", "south", "s");
        Move(Direction.West, "go west", "west", "w");

        void Move(Direction direction, params string[] forms) =>
            registry.RegisterExact((engine, session, _) =>
            {
                var location = engine.CurrentLocation(session);

                if (location is null)
                {
                    return CommandResult.NotHandled;
                }

                var target = location.Exit(direction);

                // Recorded, then Futile so the room may still answer.
                if (target <= 0)
                {
                    engine.Emit(GameEvent.For(session, EventType.BlockedMovement,
                        ("direction", direction.ToString().ToLowerInvariant())));

                    return CommandResult.Futile("You can't go that way.");
                }

                if (location.Monster is { IsAlive: true } guard && guard.Blocks(direction))
                {
                    engine.Emit(GameEvent.For(session, EventType.BlockedMovement,
                        ("direction", direction.ToString().ToLowerInvariant()), ("by", guard.Name)));

                    return CommandResult.Futile(engine.InTheDark(session) ? engine.World.Darkness!.Blocked : guard.BlockedText);
                }

                engine.MoveTo(session, target);
                return CommandResult.Done();
            }, forms);
    }

    private static void RegisterObservation(CommandRegistry registry)
    {
        registry.RegisterExact((engine, session, _) =>
        {
            session.Write(engine.DescribeCurrentLocation(session));
            return CommandResult.Done();
        }, "look", "look around", "l");

        // The room gets these words first. If nothing answers, the place is described.
        foreach (var form in new[] { "search", "explore", "investigate" })
        {
            registry.RegisterExact((engine, session, _) =>
            {
                if (!engine.TryInteractions(session, form))
                {
                    session.Write(engine.DescribeCurrentLocation(session));
                }

                return CommandResult.Done();
            }, form);
        }

        registry.RegisterExact((_, session, _) =>
        {
            var character = session.Character;
            session.Write($"HP: {character.HitPoints}/{character.MaxHitPoints}");
            return CommandResult.Done();
        }, "health", "hit points", "hp");

        // Looks at what is carried, then the creature present, then items on the
        // ground. Anything else is Futile, so the room can answer.
        registry.RegisterPrefix((engine, session, argument) =>
        {
            var name = Bare(argument);
            var location = engine.CurrentLocation(session);

            // What you carry can be felt; nothing else can be seen.
            if (engine.InTheDark(session) && session.Character.FindItemOrEquipped(name) is null)
            {
                return CommandResult.Futile("It's too dark to make anything out.");
            }

            var description =
                session.Character.FindItemOrEquipped(name)?.Description
                ?? (location?.Monster is { IsAlive: true } monster && monster.Matches(name) ? monster.Description : null)
                ?? location?.Items.FirstOrDefault(item => item.Matches(name))?.Description;

            if (description is null)
            {
                return CommandResult.Futile($"You don't see a {name} here.");
            }

            session.Write(description);
            return CommandResult.Done();
        }, "describe", "look at", "examine", "inspect");
    }

    private static void RegisterInventory(CommandRegistry registry)
    {
        registry.RegisterExact((_, session, _) =>
        {
            var character = session.Character;

            session.Write($"Weapon: {character.Weapon?.Name ?? "none equipped"}");
            session.Write($"Armor: {character.Armor?.Name ?? "none equipped"}");
            session.Write(character.Inventory.Count == 0
                ? "Inventory: empty"
                : "Inventory: " + string.Join(", ", character.Inventory.Select(item => item.Name)));

            return CommandResult.Done();
        }, "inventory", "items", "i");

        registry.RegisterPrefix((_, session, argument) =>
        {
            var character = session.Character;
            var name = Bare(argument);

            // Taking a weapon already equips it, so equipping a held item says so.
            if (character.Weapon is { } held && held.Matches(name))
            {
                session.Write($"You're already holding the {held.Name}.");
                return CommandResult.Done();
            }

            if (character.Armor is { } worn && worn.Matches(name))
            {
                session.Write($"You're already wearing the {worn.Name}.");
                return CommandResult.Done();
            }

            var item = character.FindItem(name);

            if (item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {name}.");
            }

            switch (item.Kind)
            {
                case ItemKind.Weapon:
                    if (character.Weapon is { } oldWeapon) character.Inventory.Add(oldWeapon);
                    character.Weapon = item;
                    break;

                case ItemKind.Armor:
                    if (character.Armor is { } oldArmor) character.Inventory.Add(oldArmor);
                    character.Armor = item;
                    break;

                default:
                    return CommandResult.Futile($"You can't equip the {item.Name}.");
            }

            character.Inventory.Remove(item);
            session.Write($"You equipped the {item.Name}.");
            return CommandResult.Done();
        }, "equip", "wield", "wear");

        registry.RegisterPrefix((_, session, argument) =>
        {
            var character = session.Character;

            // "use X on Y": check that X is carried, then return Futile so the room
            // decides what happens.
            var (itemName, target) = SplitTarget(argument);
            var item = character.FindItem(itemName);

            if (item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {itemName}.");
            }

            if (target is not null)
            {
                return CommandResult.Futile($"Nothing happens when you use the {item.Name} on the {target}.");
            }

            if (item.Kind != ItemKind.Consumable)
            {
                // The room may well know what this thing is for, even though the
                // engine does not.
                return CommandResult.Futile($"Nothing happens when you use the {item.Name}.");
            }

            var healed = Math.Min(item.Definition.HealAmount, character.MaxHitPoints - character.HitPoints);
            character.HitPoints += healed;

            if (item.Definition.ConsumedOnUse)
            {
                character.Inventory.Remove(item);
            }

            session.Write($"You used the {item.Name}. HP: {character.HitPoints}/{character.MaxHitPoints}");
            return CommandResult.Done();
        }, "use", "drink", "eat");

        registry.RegisterPrefix((engine, session, argument) =>
        {
            var location = engine.CurrentLocation(session);

            // "take X from Y": taking from a creature never succeeds. It is recorded
            // as tried_taking and returned as Futile so the room can answer.
            var (wanted, from) = SplitSource(argument);

            // A bare "take X", where X is held by the creature present and is not on
            // the ground, counts as the same attempt.
            var heldHere = location?.Monster is { IsAlive: true } present
                           && location.Items.All(item => !item.Matches(wanted))
                           && present.Inventory.Any(held => held.Matches(wanted));

            if ((from is not null || heldHere) && location?.Monster is { IsAlive: true } holder
                && (from is not null && holder.Matches(from) || holder.Inventory.Any(held => held.Matches(wanted))))
            {
                var held = holder.Inventory.FirstOrDefault(candidate => candidate.Matches(wanted));
                engine.Emit(GameEvent.For(session, EventType.TriedTaking,
                    ("item", held?.Name ?? wanted), ("from", holder.Name)));
                return CommandResult.Futile(held is null
                    ? $"The {holder.Name} has no {wanted}."
                    : $"The {holder.Name} won't let go of the {held.Name}.");
            }

            var name = from is null ? Bare(argument) : wanted;
            var item = location?.Items.FirstOrDefault(candidate => candidate.Matches(name));

            if (location is null || item is null)
            {
                return CommandResult.Futile($"There is no {name} here to take.");
            }

            location.Items.Remove(item);

            // A weapon or armour taken into an empty slot is equipped, as starting
            // equipment is.
            session.Write(session.Character.Receive(item)
                ? $"You take the {item.Name} and {Character.Readied(item)}."
                : $"You take the {item.Name}.");
            engine.Emit(GameEvent.For(session, EventType.ItemPickedUp, ("item", item.Name)));
            return CommandResult.Done();
        }, "take", "pick up", "get", "grab");

        registry.RegisterPrefix((engine, session, argument) =>
        {
            var location = engine.CurrentLocation(session);
            var item = session.Character.FindItem(argument);

            if (location is null || item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {argument}.");
            }

            session.Character.Inventory.Remove(item);
            location.Items.Add(item);
            session.Write($"You drop the {item.Name}.");
            engine.Emit(GameEvent.For(session, EventType.ItemDropped, ("item", item.Name)));
            return CommandResult.Done();
        }, "drop");
    }

    /// <summary>
    /// "give X to Y": checks that X is carried. The room's interactions decide
    /// whether it is taken. Recorded as gave, with whether it was taken.
    /// </summary>
    private static void RegisterGiving(CommandRegistry registry)
    {
        foreach (var verb in new[] { "give", "offer", "hand" })
        {
            registry.RegisterPrefix((engine, session, argument) =>
            {
                var split = argument.IndexOf(" to ", StringComparison.OrdinalIgnoreCase);
                var itemName = Bare(split >= 0 ? argument[..split] : argument);
                var target = split >= 0 ? Bare(argument[(split + 4)..]) : "";
                var item = session.Character.FindItemOrEquipped(itemName)
                           ?? NamedIn(session.Character, argument);

                if (item is null)
                {
                    return CommandResult.Futile($"You aren't carrying a {itemName}.");
                }

                var taken = engine.TryInteractions(session, TriggerMatcher.Normalize($"{verb} {argument}"));

                engine.Emit(GameEvent.For(session, EventType.Gave,
                    ("item", item.Name), ("to", target), ("taken", taken ? "yes" : "no")));

                return taken
                    ? CommandResult.Done()
                    : CommandResult.Futile($"Nobody here takes the {item.Name}.");
            }, verb);
        }
    }

    /// <summary>
    /// Paid for with any Currency item. The room's interactions decide what is
    /// sold. Every attempt is recorded as tried_buying.
    /// </summary>
    private static void RegisterBuying(CommandRegistry registry)
    {
        foreach (var verb in new[] { "buy", "purchase" })
        {
            registry.RegisterPrefix((engine, session, argument) =>
            {
                var at = argument.IndexOf(" with ", StringComparison.OrdinalIgnoreCase);
                var thing = Bare(at > 0 ? argument[..at] : argument);
                var canPay = session.Character.Inventory.Any(item => item.Kind == ItemKind.Currency);

                var sold = canPay && engine.TryInteractions(session, TriggerMatcher.Normalize($"{verb} {argument}"));

                engine.Emit(GameEvent.For(session, EventType.TriedBuying,
                    ("thing", thing), ("can_pay", canPay ? "yes" : "no"), ("sold", sold ? "yes" : "no")));

                if (sold)
                {
                    return CommandResult.Done();
                }

                return CommandResult.Futile(canPay
                    ? $"Nobody here is selling {thing}."
                    : "You have nothing to pay with.");
            }, verb);

            registry.RegisterExact((_, _, _) => CommandResult.Futile("Buy what?"), verb);
        }
    }

    /// <summary>
    /// A carried item whose name appears anywhere in the words, longest name
    /// first. Handles "give Y the X".
    /// </summary>
    private static Item? NamedIn(Character character, string words) =>
        character.AllPossessions()
            .OrderByDescending(item => item.Name.Length)
            .FirstOrDefault(item => Regex.IsMatch(words, $@"\b{Regex.Escape(item.Name)}\b", RegexOptions.IgnoreCase));

    /// <summary>"X on Y" -> (X, Y); no target -> (whole, null).</summary>
    private static (string Item, string? Target) SplitTarget(string argument)
    {
        foreach (var separator in new[] { " on ", " with ", " at ", " in ", " against " })
        {
            var at = argument.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (at > 0)
            {
                return (Bare(argument[..at]), Bare(argument[(at + separator.Length)..]));
            }
        }
        return (Bare(argument), null);
    }

    /// <summary>"X from Y" -> (X, Y); no source -> (whole, null).</summary>
    private static (string Wanted, string? From) SplitSource(string argument)
    {
        foreach (var separator in new[] { " from ", " off of ", " off " })
        {
            var at = argument.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (at > 0)
            {
                return (Bare(argument[..at]), Bare(argument[(at + separator.Length)..]));
            }
        }
        return (Bare(argument), null);
    }

    /// <summary>Drops a leading article.</summary>
    private static string Bare(string name)
    {
        var trimmed = name.Trim();
        foreach (var article in new[] { "the ", "a ", "an " })
        {
            if (trimmed.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[article.Length..].Trim();
            }
        }
        return trimmed;
    }

    private static void RegisterCombat(CommandRegistry registry)
    {
        CommandResult Strike(GameEngine engine, Session session, string argument)
        {
            var location = engine.CurrentLocation(session);

            if (location?.Monster is not { IsAlive: true } monster)
            {
                return CommandResult.Futile("There is nothing to attack here.");
            }

            // "attack X with Y" targets X. Blows are always struck with the equipped weapon.
            var target = Bare(argument);
            foreach (var separator in new[] { " with ", " using " })
            {
                var at = target.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
                if (at > 0)
                {
                    target = target[..at].Trim();
                }
            }

            if (target.Length > 0 && !monster.Matches(target))
            {
                return CommandResult.Futile($"There is no {target} here to attack.");
            }

            engine.Attack(session);
            return CommandResult.Done();
        }

        registry.RegisterExact(Strike, "attack", "fight", "kill", "stab");
        registry.RegisterPrefix(Strike, "attack", "fight", "kill", "stab");
    }

    private static void RegisterLeaving(CommandRegistry registry)
    {
        registry.RegisterExact((engine, session, _) =>
        {
            var location = engine.CurrentLocation(session);

            if (location is null || !location.IsExit)
            {
                return CommandResult.Futile("Sadly, this is not the exit.");
            }

            return engine.Escape(session)
                ? CommandResult.Done()
                : CommandResult.Futile(engine.World.ExitNeedsKey ? "You don't have a key." : "You can't leave now.");
        }, "exit", "escape", "leave");
    }

    private static void RegisterSpeech(CommandRegistry registry)
    {
        // Speech is recorded. The world itself never answers.
        registry.RegisterPrefix((engine, session, argument) =>
        {
            session.Write($"You say: {argument}");
            engine.Emit(GameEvent.For(session, EventType.Speech, ("mode", "say"), ("text", argument)));
            return CommandResult.Done();
        }, "say", "speak");

        registry.RegisterPrefix((engine, session, argument) =>
        {
            session.Write($"You yell: {argument}");
            engine.Emit(GameEvent.For(session, EventType.Speech, ("mode", "yell"), ("text", argument)));
            return CommandResult.Done();
        }, "yell", "shout");

        registry.RegisterPrefix((engine, session, argument) =>
        {
            session.Write("You pray.");
            engine.Emit(GameEvent.For(session, EventType.Speech, ("mode", "pray"), ("text", argument)));
            return CommandResult.Done();
        }, "pray");

        registry.RegisterExact((engine, session, _) =>
        {
            session.Write("You pray.");
            engine.Emit(GameEvent.For(session, EventType.Speech, ("mode", "pray"), ("text", "")));
            return CommandResult.Done();
        }, "pray");
    }
}
