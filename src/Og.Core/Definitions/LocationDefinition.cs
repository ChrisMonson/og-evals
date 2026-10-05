namespace Og.Core.Definitions;

public sealed class ConnectionsDefinition
{
    public int North { get; set; }
    public int East { get; set; }
    public int South { get; set; }
    public int West { get; set; }
}

public sealed class LocationDefinition
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ConnectionsDefinition Connections { get; set; } = new();
    public bool IsExit { get; set; }

    /// <summary>Dark without a light: see the world's darkness for what that means.</summary>
    public bool IsDark { get; set; }

    /// <summary>What can be made out here without a light, if more than the world's darkness says.</summary>
    public string? DarkDescription { get; set; }
    public string? Monster { get; set; }

    /// <summary>Items lying here when the world begins.</summary>
    public string[] Ground { get; set; } = [];
    public InteractionDefinition[] Interactions { get; set; } = [];
}
