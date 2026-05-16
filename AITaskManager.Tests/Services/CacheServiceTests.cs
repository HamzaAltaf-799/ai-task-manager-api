using AITaskManager.API.Services;
using AITaskManager.Tests.Helpers;

namespace AITaskManager.Tests.Services;

public static class CacheServiceTests
{
    public static void RunAll()
    {
        Console.WriteLine("\n[CacheService]");
        Set_ShouldStoreValue();
        Get_ShouldReturnNull_ForMissingKey();
        Get_ShouldReturnNull_AfterExpiry();
        Remove_ShouldDeleteKey();
        RemoveByPrefix_ShouldDeleteMatchingKeys();
    }

    private static void Set_ShouldStoreValue()
    {
        var cache = new CacheService();
        cache.Set("key1", "hello");
        var result = cache.Get<string>("key1");
        Assert.Equal("hello", result, "Cache: stored value retrieved");
        Console.WriteLine("  ✅ Set stores value, Get retrieves it");
    }

    private static void Get_ShouldReturnNull_ForMissingKey()
    {
        var cache  = new CacheService();
        var result = cache.Get<string>("nonexistent");
        Assert.Null(result, "Cache: missing key returns null");
        Console.WriteLine("  ✅ Get returns null for missing key");
    }

    private static void Get_ShouldReturnNull_AfterExpiry()
    {
        var cache = new CacheService();
        cache.Set("expiring", "value", TimeSpan.FromMilliseconds(10));
        Thread.Sleep(20);
        var result = cache.Get<string>("expiring");
        Assert.Null(result, "Cache: expired entry returns null");
        Console.WriteLine("  ✅ Get returns null after TTL expiry");
    }

    private static void Remove_ShouldDeleteKey()
    {
        var cache = new CacheService();
        cache.Set("toRemove", 42);
        cache.Remove("toRemove");
        var result = cache.Get<int?>("toRemove");
        Assert.Null(result, "Cache: removed key returns null");
        Console.WriteLine("  ✅ Remove deletes key");
    }

    private static void RemoveByPrefix_ShouldDeleteMatchingKeys()
    {
        var cache = new CacheService();
        cache.Set("tasks:user1:list", "list1");
        cache.Set("tasks:user1:count", "count1");
        cache.Set("tasks:user2:list", "list2"); // Different prefix — should survive
        cache.Set("task:user1:abc", "task");    // Different prefix — should survive

        cache.RemoveByPrefix("tasks:user1");

        Assert.Null(cache.Get<string>("tasks:user1:list"),  "Prefix: list1 removed");
        Assert.Null(cache.Get<string>("tasks:user1:count"), "Prefix: count1 removed");
        Assert.NotNull(cache.Get<string>("tasks:user2:list"), "Prefix: user2 list untouched");
        Assert.NotNull(cache.Get<string>("task:user1:abc"), "Prefix: different prefix untouched");
        Console.WriteLine("  ✅ RemoveByPrefix only removes matching keys");
    }
}
