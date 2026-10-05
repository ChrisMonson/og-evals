namespace Og.Core.Runtime;

public enum CommandStatus
{
    /// <summary>No handler wanted it.</summary>
    NotHandled,

    /// <summary>A handler did something. The command is spent.</summary>
    Done,

    /// <summary>
    /// A handler recognised the words and could do nothing with them: nothing to
    /// take, nothing to attack, nothing of that name being carried.
    ///
    /// This is not the same as being handled. The command carries on to the place
    /// the player is standing in, which may well have an answer the engine does
    /// not — someone typing "take belongings" in a room that describes belongings
    /// should reach that room rather than being told they are carrying nothing.
    /// </summary>
    Futile
}

public readonly record struct CommandResult(CommandStatus Status, string? Message = null)
{
    public static readonly CommandResult NotHandled = new(CommandStatus.NotHandled);

    public static CommandResult Done() => new(CommandStatus.Done);

    /// <summary>
    /// Nothing came of it. The message is held back rather than written, and is
    /// used only if the place has nothing to say either.
    /// </summary>
    public static CommandResult Futile(string message) => new(CommandStatus.Futile, message);
}
