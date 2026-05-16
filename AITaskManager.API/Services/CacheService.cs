using System.Collections.Concurrent;
using AITaskManager.API.Interfaces;

namespace AITaskManager.API.Services;

/// <summary>
/// Simple in-process cache with optional TTL.
/// Drop-in replace this with a Redis-backed implementation via the ICacheService contract.
/// </summary>
public class CacheService : ICacheService
{
    private sealed record CacheEntry(object Value, DateTime? ExpiresAt);

    private readonly ConcurrentDictionary<string, CacheEntry> _store = new();

    public T? Get<T>(string key)
    {
        if (!_store.TryGetValue(key, out var entry)) return default;
        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
        {
            _store.TryRemove(key, out _);
            return default;
        }
        return (T)entry.Value;
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        if (value is null) return;
        var expiresAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
        _store[key] = new CacheEntry(value, expiresAt);
    }

    public void Remove(string key) => _store.TryRemove(key, out _);

    public void RemoveByPrefix(string prefix)
    {
        foreach (var key in _store.Keys.Where(k => k.StartsWith(prefix)))
            _store.TryRemove(key, out _);
    }
}
