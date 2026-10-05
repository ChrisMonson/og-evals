namespace Og.Core.Definitions;

/// <summary>
/// A limit on moves. Each move between places costs one, and spendtime can cost
/// more. The time left is shown with each place description.
/// </summary>
public sealed class ClockDefinition
{
    /// <summary>Moves a player may make. The next one, past these, ends the session.</summary>
    public int Moves { get; set; }

    /// <summary>Shown with each place: {0} is the moves left, {1} is "s" unless that is one.</summary>
    public string Status { get; set; } = "{0} move{1} left.";

    /// <summary>Said when a player tries to move with none left. Nothing more happens after it.</summary>
    public string Expired { get; set; } = "Time has run out.";

    /// <summary>A condition that, once set, stops the clock: moves are no longer counted or shown.</summary>
    public string? StopsWhen { get; set; }
}
