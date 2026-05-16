namespace AITaskManager.Tests.Helpers;

/// <summary>
/// Minimal assertion library — same semantics as xunit's Assert class.
/// Replace with xunit + FluentAssertions once connected to NuGet.
/// </summary>
public static class Assert
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> _failures = [];

    public static void Equal<T>(T expected, T actual, string context = "")
    {
        if (Equals(expected, actual)) { Pass(); return; }
        Fail($"{context} Expected <{expected}> but got <{actual}>");
    }

    public static void NotNull(object? value, string context = "")
    {
        if (value is not null) { Pass(); return; }
        Fail($"{context} Expected non-null value.");
    }

    public static void Null(object? value, string context = "")
    {
        if (value is null) { Pass(); return; }
        Fail($"{context} Expected null but got <{value}>");
    }

    public static void True(bool condition, string context = "")
    {
        if (condition) { Pass(); return; }
        Fail($"{context} Expected true.");
    }

    public static void False(bool condition, string context = "")
    {
        if (!condition) { Pass(); return; }
        Fail($"{context} Expected false.");
    }

    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate, string context = "")
    {
        if (collection.Any(predicate)) { Pass(); return; }
        Fail($"{context} Collection does not contain expected element.");
    }

    public static void Count<T>(IEnumerable<T> collection, int expected, string context = "")
    {
        var actual = collection.Count();
        if (actual == expected) { Pass(); return; }
        Fail($"{context} Expected count {expected} but got {actual}.");
    }

    public static void Summary()
    {
        Console.WriteLine();
        Console.WriteLine($"═══════════════════════════════════════");
        Console.WriteLine($"  Tests: {_passed + _failed}  ✅ Passed: {_passed}  ❌ Failed: {_failed}");
        Console.WriteLine($"═══════════════════════════════════════");

        if (_failures.Count > 0)
        {
            Console.WriteLine("\nFailures:");
            foreach (var f in _failures)
                Console.WriteLine($"  • {f}");
            Environment.Exit(1);
        }
    }

    private static void Pass() => Interlocked.Increment(ref _passed);
    private static void Fail(string msg)
    {
        Interlocked.Increment(ref _failed);
        _failures.Add(msg);
        Console.WriteLine($"  ❌ {msg}");
    }
}
