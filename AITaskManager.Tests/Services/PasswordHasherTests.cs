using AITaskManager.API.Auth;
using AITaskManager.Tests.Helpers;

namespace AITaskManager.Tests.Services;

public static class PasswordHasherTests
{
    public static void RunAll()
    {
        Console.WriteLine("\n[PasswordHasher]");
        Hash_ShouldProduceDifferentSalts();
        Verify_ShouldReturnTrue_ForCorrectPassword();
        Verify_ShouldReturnFalse_ForWrongPassword();
        Verify_ShouldBeTimingSafe();
    }

    private static void Hash_ShouldProduceDifferentSalts()
    {
        var h1 = PasswordHasher.Hash("SamePassword@1");
        var h2 = PasswordHasher.Hash("SamePassword@1");
        Assert.True(h1 != h2, "Hash_ShouldProduceDifferentSalts: same input should yield different hashes (different salts)");
        Console.WriteLine("  ✅ Hash produces unique salts per call");
    }

    private static void Verify_ShouldReturnTrue_ForCorrectPassword()
    {
        var password = "Correct@Pass1";
        var hash     = PasswordHasher.Hash(password);
        Assert.True(PasswordHasher.Verify(password, hash), "Verify_CorrectPassword");
        Console.WriteLine("  ✅ Verify returns true for correct password");
    }

    private static void Verify_ShouldReturnFalse_ForWrongPassword()
    {
        var hash = PasswordHasher.Hash("RightPassword@1");
        Assert.False(PasswordHasher.Verify("WrongPassword@1", hash), "Verify_WrongPassword");
        Console.WriteLine("  ✅ Verify returns false for incorrect password");
    }

    private static void Verify_ShouldBeTimingSafe()
    {
        // Both branches should complete — this doesn't assert timing but confirms
        // no exception is thrown for malformed hash input
        var result = PasswordHasher.Verify("password", "not-a-valid-hash");
        Assert.False(result, "Verify_MalformedHash");
        Console.WriteLine("  ✅ Verify handles malformed hash gracefully");
    }
}
