using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// A living character's presence in the world, and the channel its text goes to.
/// A session begins at birth and ends at death.
/// </summary>
public sealed class Session(Character character, IOutputSink output)
{
    public Character Character { get; } = character;
    public IOutputSink Output { get; } = output;

    public string CharacterId => Character.Id;

    public int CurrentLocationId
    {
        get => Character.CurrentLocationId;
        set => Character.CurrentLocationId = value;
    }

    /// <summary>Moves made, against the world's clock.</summary>
    public int Moves { get; set; }

    /// <summary>The clock ran out. Like death, it ends the session; unlike death, the character lives.</summary>
    public bool OutOfTime { get; set; }

    /// <summary>Ended by an endgame action. Nothing more happens.</summary>
    public bool Ended { get; set; }

    /// <summary>Whether anything more can happen in this session.</summary>
    public bool Over => OutOfTime || Ended || Character.IsDead;

    public void Write(string message) => Output.Write(message);
}
