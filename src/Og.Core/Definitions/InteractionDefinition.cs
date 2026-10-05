using Og.Core.Model;

namespace Og.Core.Definitions;

public sealed class InteractionDefinition
{
    public string? InteractionId { get; set; }
    public string[] Triggers { get; set; } = [];
    public ActionDefinition[] Actions { get; set; } = [];
    public string[] If { get; set; } = [];
    public string[] IfNot { get; set; } = [];
    public bool Disabled { get; set; }

    /// <summary>
    /// World: enabled state belongs to the world. Player: it belongs to the
    /// character, and is lost with it.
    /// </summary>
    public Scope Scope { get; set; } = Scope.World;

    /// <summary>Whether the interaction fires only once per character.</summary>
    public bool Once { get; set; }
}
