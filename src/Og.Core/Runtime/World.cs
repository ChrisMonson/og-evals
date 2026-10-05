using System.Text.Json;
using System.Text.Json.Serialization;
using Og.Core.Definitions;
using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>Runtime world state, built from a WorldDefinition.</summary>
public sealed class World
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public const string CarryingPrefix = "carrying:";
    public const string SlainPrefix = "slain:";

    private readonly Dictionary<int, Location> _locations = [];
    private readonly Dictionary<int, string[]> _authoredGround = [];
    private readonly Dictionary<string, ItemDefinition> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MonsterDefinition> _monsters = new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; }
    public int SpawnLocation { get; }
    public IReadOnlyList<string> StartingItems { get; }
    public ClockDefinition? Clock { get; }
    public DarknessDefinition? Darkness { get; }
    public bool ExitNeedsKey { get; }
    public bool ExitOnArrival { get; }

    /// <summary>Creatures killed, by name, for slain: conditions.</summary>
    public HashSet<string> Slain { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>World-scoped condition flags.</summary>
    public HashSet<string> Conditions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<Location> Locations => _locations.Values;

    private World(WorldDefinition definition)
    {
        Name = definition.Name;
        SpawnLocation = definition.SpawnLocation;
        StartingItems = definition.StartingItems;
        Clock = definition.Clock;
        Darkness = definition.Darkness;
        ExitNeedsKey = definition.ExitNeedsKey;
        ExitOnArrival = definition.ExitOnArrival;

        foreach (var item in definition.Items)
        {
            _items[item.Name] = item;
        }

        foreach (var monster in definition.Monsters)
        {
            _monsters[monster.Name] = monster;
        }

        var words = new Dictionary<string, string[]>(definition.Words, StringComparer.OrdinalIgnoreCase);

        foreach (var locationDefinition in definition.Locations)
        {
            var location = new Location(locationDefinition);

            foreach (var interaction in locationDefinition.Interactions)
            {
                location.Interactions.Add(new Interaction(interaction, words));
            }

            location.Items.AddRange(locationDefinition.Ground.Select(CreateItem).OfType<Item>());

            if (locationDefinition.Monster is { Length: > 0 } monsterName)
            {
                location.Monster = SpawnMonster(monsterName);
            }

            _locations[location.Id] = location;
            _authoredGround[location.Id] = locationDefinition.Ground;
        }

        if (SpawnLocation == 0 && _locations.Count > 0)
        {
            SpawnLocation = _locations.Keys.Min();
        }
    }

    public static World FromJson(string json)
    {
        var definition = JsonSerializer.Deserialize<WorldDefinition>(json, JsonOptions)
            ?? throw new InvalidDataException("World definition did not deserialize.");

        return new World(definition);
    }

    public static World Load(string path) => FromJson(File.ReadAllText(path));

    /// <summary>The item names a place was written with, whether or not they exist.</summary>
    public IReadOnlyList<string> GroundOf(int locationId) =>
        _authoredGround.TryGetValue(locationId, out var names) ? names : [];

    public Location? FindLocation(int id) =>
        _locations.TryGetValue(id, out var location) ? location : null;

    public ItemDefinition? FindItemDefinition(string name) =>
        _items.TryGetValue(name.Trim(), out var definition) ? definition : null;

    /// <summary>Creates an item instance, or null if no definition by that name exists.</summary>
    public Item? CreateItem(string name) =>
        FindItemDefinition(name) is { } definition ? new Item(definition) : null;

    public Monster? SpawnMonster(string name)
    {
        if (!_monsters.TryGetValue(name.Trim(), out var definition))
        {
            return null;
        }

        var inventory = definition.Inventory
            .Select(CreateItem)
            .OfType<Item>();

        return new Monster(definition, inventory);
    }

    public bool HasMonsterDefinition(string name) => _monsters.ContainsKey(name.Trim());

    /// <summary>
    /// A condition holds if it is set on the world or on the living character;
    /// <c>carrying:&lt;item&gt;</c> holds while the character has that item, and
    /// <c>slain:&lt;creature&gt;</c> once that creature has been killed.
    /// </summary>
    public bool HasCondition(Session session, string condition)
    {
        if (condition.StartsWith(CarryingPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return session.Character.FindItemOrEquipped(condition[CarryingPrefix.Length..].Trim()) is not null;
        }

        if (condition.StartsWith(SlainPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Slain.Contains(condition[SlainPrefix.Length..].Trim());
        }

        return Conditions.Contains(condition) || session.Character.Conditions.Contains(condition);
    }

    public IEnumerable<Interaction> AllInteractions() =>
        _locations.Values.SelectMany(location => location.Interactions);

    public Interaction? FindInteraction(string id) =>
        AllInteractions().FirstOrDefault(interaction =>
            string.Equals(interaction.Id, id, StringComparison.OrdinalIgnoreCase));
}
