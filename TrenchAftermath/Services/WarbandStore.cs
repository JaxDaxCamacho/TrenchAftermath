using System.Text.Json;
using Microsoft.JSInterop;
using TrenchAftermath.Domain;

namespace TrenchAftermath.Services;

public sealed record SavedWarbandRef(string Id, string Name, DateTime SavedAt);

public sealed class WarbandStore
{
    private readonly IJSRuntime _js;
    private const string IndexKey = "trench-warband-index";
    private const string WarbandPrefix = "trench-warband:";

    public WarbandStore(IJSRuntime js) => _js = js;

    public async Task<List<SavedWarbandRef>> ListAsync()
    {
        var raw = await _js.InvokeAsync<string?>("localStorage.getItem", IndexKey);
        if (string.IsNullOrWhiteSpace(raw)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<SavedWarbandRef>>(raw) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<SavedWarbandRef> SaveAsync(WarbandSession session)
    {
        var id = GetId(session);
        var name = session.WarbandName;
        var json = session.Serialize();
        await _js.InvokeVoidAsync("localStorage.setItem", WarbandPrefix + id, json);

        var list = await ListAsync();
        list.RemoveAll(r => r.Id == id);
        var entry = new SavedWarbandRef(id, name, DateTime.UtcNow);
        list.Insert(0, entry);
        await _js.InvokeVoidAsync("localStorage.setItem", IndexKey, JsonSerializer.Serialize(list));
        return entry;
    }

    public async Task<WarbandSession?> LoadAsync(string id)
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", WarbandPrefix + id);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return WarbandSession.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task DeleteAsync(string id)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", WarbandPrefix + id);
        var list = await ListAsync();
        list.RemoveAll(r => r.Id == id);
        await _js.InvokeVoidAsync("localStorage.setItem", IndexKey, JsonSerializer.Serialize(list));
    }

    private static string GetId(WarbandSession session)
    {
        var raw = session.Root["warband-id"];
        if (raw is not null) return raw.ToJsonString().Trim('"');
        return "wb_" + Guid.NewGuid().ToString("N")[..8];
    }
}
