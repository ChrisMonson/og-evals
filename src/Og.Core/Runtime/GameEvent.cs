namespace Og.Core.Runtime;

public static class EventType
{
    public const string Command = "command";
    public const string UnparsedCommand = "unparsed_command";
    public const string Movement = "movement";
    public const string BlockedMovement = "blocked_movement";
    public const string Combat = "combat";
    public const string Death = "death";
    public const string CharacterBorn = "character_born";
    public const string MonsterSlain = "monster_slain";
    public const string ItemPickedUp = "item_picked_up";
    public const string ItemDropped = "item_dropped";
    public const string InteractionFired = "interaction_fired";
    public const string Speech = "speech";
    public const string Arrived = "arrived";
    public const string Departed = "departed";
    public const string Escaped = "escaped";
    public const string TimeRanOut = "time_ran_out";
    public const string TriedBuying = "tried_buying";
    public const string Fell = "fell";
    public const string GameEnded = "game_ended";

    /// <summary>Something offered to whatever is in the room; payload says if it was taken.</summary>
    public const string Gave = "gave";

    /// <summary>A monster removed alive by removemonster.</summary>
    public const string MonsterLeft = "monster_left";

    /// <summary>An attempt to take something a creature is holding. It does not succeed.</summary>
    public const string TriedTaking = "tried_taking";
}

/// <summary>
/// One row in the world's event log. Events are keyed by the character that acted
/// and the location it acted in.
/// </summary>
public sealed record GameEvent(
    string Actor,
    string Type,
    string? CharacterId,
    int? LocationId,
    IReadOnlyDictionary<string, string> Payload)
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public static GameEvent For(
        Session session,
        string type,
        params (string Key, string Value)[] payload) =>
        new(session.CharacterId,
            type,
            session.CharacterId,
            session.CurrentLocationId,
            payload.ToDictionary(entry => entry.Key, entry => entry.Value));
}

public interface IEventSink
{
    void Emit(GameEvent gameEvent);
}

public sealed class CollectingEventSink : IEventSink
{
    public List<GameEvent> Events { get; } = [];

    public void Emit(GameEvent gameEvent) => Events.Add(gameEvent);
}
