namespace Og.Core.Definitions;

public sealed class WorldDefinition
{
    public string Name { get; set; } = "world";

    /// <summary>Location id where new characters appear.</summary>
    public int SpawnLocation { get; set; }

    /// <summary>Items every new character arrives carrying. Equipment is auto-equipped.</summary>
    public string[] StartingItems { get; set; } = [];

    public ItemDefinition[] Items { get; set; } = [];
    public MonsterDefinition[] Monsters { get; set; } = [];
    public LocationDefinition[] Locations { get; set; } = [];

    /// <summary>A limit on moves, if the world has one.</summary>
    public ClockDefinition? Clock { get; set; }

    /// <summary>What dark places are like without a light. Needed if any place is dark.</summary>
    public DarknessDefinition? Darkness { get; set; }

    /// <summary>Whether leaving requires carrying an IsKey item.</summary>
    public bool ExitNeedsKey { get; set; } = true;

    /// <summary>Whether entering an exit location escapes immediately, without the exit command.</summary>
    public bool ExitOnArrival { get; set; }

    /// <summary>Named word groups that triggers refer to in braces: "{give} *&amp;*bread*".</summary>
    public Dictionary<string, string[]> Words { get; set; } = [];
}
