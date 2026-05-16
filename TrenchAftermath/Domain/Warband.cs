using System.Text.Json;
using System.Text.Json.Nodes;

namespace TrenchAftermath.Domain;

// Backed by a JsonNode tree so unknown fields round-trip losslessly. The shape
// follows the trench-companion.com export. We add a few additive campaign-state
// keys on export (e.g. consecutive-failed-promotion-dice).
public sealed class WarbandSession
{
    public JsonObject Root { get; }
    public IReadOnlyList<ModelEntry> Models { get; }

    public string WarbandName => Root["warband-name"]?.GetValue<string>() ?? "Unnamed warband";

    public int DucatBank => GetInt("ducat-bank");
    public int GloryBank => GetInt("glory-bank");
    public int DucatRating => GetInt("ducat-rating");

    public int ConsecutiveFailedPromotionDice
    {
        get => GetInt("consecutive-failed-promotion-dice");
        set => Root["consecutive-failed-promotion-dice"] = value;
    }

    private WarbandSession(JsonObject root, IReadOnlyList<ModelEntry> models)
    {
        Root = root;
        Models = models;
    }

    public static WarbandSession Parse(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidDataException("Empty JSON.");
        if (node is not JsonObject root) throw new InvalidDataException("Expected a JSON object at the root.");

        var modelsNode = root["models"]
            ?? throw new InvalidDataException("Missing 'models' array on warband.");
        if (modelsNode is not JsonArray modelsArr)
            throw new InvalidDataException("'models' must be a JSON array.");

        var entries = new List<ModelEntry>(modelsArr.Count);
        for (var i = 0; i < modelsArr.Count; i++)
        {
            var m = modelsArr[i] as JsonObject
                ?? throw new InvalidDataException($"Model at index {i} is not an object.");
            entries.Add(new ModelEntry(m, i));
        }

        return new WarbandSession(root, entries);
    }

    public string Serialize() => Root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private int GetInt(string key) => Root[key] is JsonNode n ? n.GetValue<int>() : 0;
}

public sealed class ModelEntry
{
    public JsonObject Node { get; }
    public int Index { get; }

    public ModelEntry(JsonObject node, int index)
    {
        Node = node;
        Index = index;
    }

    public string Name =>
        Node["name"]?.GetValue<string>()
        ?? Node["model-name"]?.GetValue<string>()
        ?? "Unknown";

    public string ModelId => Node["model-id"]?.GetValue<string>() ?? "";

    public bool IsElite => HasKeyword("kw_elite");

    public string StatMove   => Node["stat-move"]?.GetValue<string>()   ?? "";
    public string StatMelee  => Node["stat-melee"]?.GetValue<string>()  ?? "";
    public string StatRanged => Node["stat-ranged"]?.GetValue<string>() ?? "";
    public string StatArmour => Node["stat-armour"]?.GetValue<string>() ?? "";

    public IEnumerable<string> Equipment => Names(Node["equipment"], "equipment-name");
    public IEnumerable<string> Abilities => Names(Node["abilities"], "ability-name");
    public IEnumerable<string> Keywords  => Names(Node["keywords"],  "keyword-name");
    public IEnumerable<string> Injuries  => Names(Node["injuries"],  "injury-name", fallbackKey: "name");

    private static IEnumerable<string> Names(JsonNode? listNode, string key, string? fallbackKey = null)
    {
        if (listNode is not JsonArray arr) yield break;
        foreach (var item in arr)
        {
            if (item is JsonObject obj)
            {
                var name = obj[key]?.GetValue<string>();
                if (name is null && fallbackKey is not null)
                    name = obj[fallbackKey]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(name)) yield return name;
            }
            else if (item is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrWhiteSpace(s))
            {
                yield return s;
            }
        }
    }

    public bool HasKeyword(string keywordId)
    {
        if (Node["keywords"] is not JsonArray kws) return false;
        foreach (var k in kws)
        {
            if (k is JsonObject obj && obj["keyword-id"]?.GetValue<string>() == keywordId)
                return true;
        }
        return false;
    }

    public void AddKeyword(string name, string id)
    {
        if (HasKeyword(id)) return;
        var kws = Node["keywords"] as JsonArray;
        if (kws is null)
        {
            kws = new JsonArray();
            Node["keywords"] = kws;
        }
        kws.Add(new JsonObject
        {
            ["keyword-name"] = name,
            ["keyword-id"] = id,
        });
    }

    // Adds a stamped entry to the model's "advancements" array. Used when we
    // promote a Troop so the change is traceable on export.
    public void RecordAdvancement(string title, string detail)
    {
        var arr = Node["advancements"] as JsonArray;
        if (arr is null)
        {
            arr = new JsonArray();
            Node["advancements"] = arr;
        }
        arr.Add(new JsonObject
        {
            ["title"] = title,
            ["detail"] = detail,
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
        });
    }
}
