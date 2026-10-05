using Og.Core.Model;

namespace Og.Core.Definitions;

public sealed class ActionDefinition
{
    public string Action { get; set; } = "";
    public string[] Parameters { get; set; } = [];

    /// <summary>
    /// For conditions and enabling or disabling interactions: whether the effect
    /// belongs to the world or to the acting character.
    /// </summary>
    public Scope Scope { get; set; } = Scope.World;
}
