using AITaskManager.API.Auth;
using AITaskManager.API.Configurations;
using AITaskManager.API.Data;
using AITaskManager.API.DTOs.Auth;
using AITaskManager.API.Repositories;
using AITaskManager.API.Services;
using AITaskManager.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AITaskManager.Tests.Services;

public static class AuthServiceTests
{
    public static void RunAll()
    {
        Console.WriteLine("\n[AuthService]");
        Register_ShouldCreateUser_AndReturnToken();
        Register_ShouldFail_ForDuplicateEmail();
        Register_ShouldFail_ForWeakPassword();
        Login_ShouldSucceed_ForValidCredentials();
        Login_ShouldFail_ForWrongPassword();
        Login_ShouldFail_ForUnknownEmail();
        Login_ShouldFail_ForInactiveUser();
        Login_ShouldUpdateLastLoginAt();
    }

    private static AuthService CreateSut()
    {
        var db      = new InMemoryDatabase();
        var repo    = new UserRepository(db);
        var options = Options.Create(new JwtSettings
        {
            Secret         = "test-secret-key-must-be-at-least-32-chars!!",
            Issuer         = "TestIssuer",
            Audience       = "TestAudience",
            ExpiryMinutes  = 60
        });
        var jwt    = new JwtTokenService(options, NullLogger<JwtTokenService>.Instance);
        var logger = NullLogger<AuthService>.Instance;
        return new AuthService(repo, jwt, logger);
    }

    private static void Register_ShouldCreateUser_AndReturnToken()
    {
        var svc = CreateSut();
        var req = new RegisterRequest
        {
            FirstName = "Jane", LastName = "Doe",
            Email     = "jane@example.com",
            Password  = "Secure@Pass1"
        };

        var result = svc.RegisterAsync(req).GetAwaiter().GetResult();

        Assert.NotNull(result, "Register: result");
        Assert.NotNull(result!.AccessToken, "Register: access token");
        Assert.Equal("Bearer", result.TokenType, "Register: token type");
        Assert.Equal("jane@example.com", result.User.Email, "Register: user email");
        Assert.Equal("User", result.User.Role, "Register: default role");
        Console.WriteLine("  ✅ Register creates user and returns valid token");
    }

    private static void Register_ShouldFail_ForDuplicateEmail()
    {
        var svc = CreateSut();
        var req = new RegisterRequest
        {
            FirstName = "Jane", LastName = "Doe",
            Email     = "duplicate@example.com",
            Password  = "Secure@Pass1"
        };

        svc.RegisterAsync(req).GetAwaiter().GetResult(); // First registration
        var second = svc.RegisterAsync(req).GetAwaiter().GetResult(); // Should fail

        Assert.Null(second, "Register: duplicate email returns null");
        Console.WriteLine("  ✅ Register rejects duplicate email");
    }

    private static void Register_ShouldFail_ForWeakPassword()
    {
        var svc = CreateSut();
        var req = new RegisterRequest
        {
            FirstName = "Test", LastName = "User",
            Email     = "test@example.com",
            Password  = "weak"   // Too short, no uppercase, no digit, no special
        };

        var result = svc.RegisterAsync(req).GetAwaiter().GetResult();
        Assert.Null(result, "Register: weak password returns null");
        Console.WriteLine("  ✅ Register rejects weak password");
    }

    private static void Login_ShouldSucceed_ForValidCredentials()
    {
        var svc = CreateSut();
        svc.RegisterAsync(new RegisterRequest
        {
            FirstName = "John", LastName = "Smith",
            Email = "john@example.com", Password = "Secure@Pass1"
        }).GetAwaiter().GetResult();

        var result = svc.LoginAsync(new LoginRequest
        {
            Email    = "john@example.com",
            Password = "Secure@Pass1"
        }).GetAwaiter().GetResult();

        Assert.NotNull(result, "Login: result");
        Assert.NotNull(result!.AccessToken, "Login: access token");
        Assert.Equal("john@example.com", result.User.Email, "Login: correct user");
        Console.WriteLine("  ✅ Login succeeds for valid credentials");
    }

    private static void Login_ShouldFail_ForWrongPassword()
    {
        var svc = CreateSut();
        svc.RegisterAsync(new RegisterRequest
        {
            FirstName = "A", LastName = "B",
            Email = "a@example.com", Password = "Correct@Pass1"
        }).GetAwaiter().GetResult();

        var result = svc.LoginAsync(new LoginRequest
        {
            Email = "a@example.com", Password = "Wrong@Pass1"
        }).GetAwaiter().GetResult();

        Assert.Null(result, "Login: wrong password returns null");
        Console.WriteLine("  ✅ Login rejects wrong password");
    }

    private static void Login_ShouldFail_ForUnknownEmail()
    {
        var svc    = CreateSut();
        var result = svc.LoginAsync(new LoginRequest
        {
            Email = "nobody@example.com", Password = "Any@Pass1"
        }).GetAwaiter().GetResult();

        Assert.Null(result, "Login: unknown email returns null");
        Console.WriteLine("  ✅ Login rejects unknown email");
    }

    private static void Login_ShouldFail_ForInactiveUser()
    {
        var db      = new InMemoryDatabase();
        var repo    = new UserRepository(db);
        var options = Options.Create(new JwtSettings
        {
            Secret = "test-secret-key-must-be-at-least-32-chars!!",
            Issuer = "TestIssuer", Audience = "TestAudience", ExpiryMinutes = 60
        });
        var jwt = new JwtTokenService(options, NullLogger<JwtTokenService>.Instance);
        var svc = new AuthService(repo, jwt, NullLogger<AuthService>.Instance);

        // Register then deactivate
        svc.RegisterAsync(new RegisterRequest
        {
            FirstName = "Inactive", LastName = "User",
            Email = "inactive@example.com", Password = "Secure@Pass1"
        }).GetAwaiter().GetResult();

        var user = repo.GetByEmailAsync("inactive@example.com").GetAwaiter().GetResult()!;
        user.IsActive = false;
        repo.UpdateAsync(user).GetAwaiter().GetResult();

        var result = svc.LoginAsync(new LoginRequest
        {
            Email = "inactive@example.com", Password = "Secure@Pass1"
        }).GetAwaiter().GetResult();

        Assert.Null(result, "Login: inactive user returns null");
        Console.WriteLine("  ✅ Login rejects inactive account");
    }

    private static void Login_ShouldUpdateLastLoginAt()
    {
        var db      = new InMemoryDatabase();
        var repo    = new UserRepository(db);
        var options = Options.Create(new JwtSettings
        {
            Secret = "test-secret-key-must-be-at-least-32-chars!!",
            Issuer = "I", Audience = "A", ExpiryMinutes = 60
        });
        var jwt = new JwtTokenService(options, NullLogger<JwtTokenService>.Instance);
        var svc = new AuthService(repo, jwt, NullLogger<AuthService>.Instance);

        svc.RegisterAsync(new RegisterRequest
        {
            FirstName = "Track", LastName = "Login",
            Email = "track@example.com", Password = "Secure@Pass1"
        }).GetAwaiter().GetResult();

        svc.LoginAsync(new LoginRequest
            { Email = "track@example.com", Password = "Secure@Pass1" })
           .GetAwaiter().GetResult();

        var user = repo.GetByEmailAsync("track@example.com").GetAwaiter().GetResult()!;
        Assert.NotNull(user.LastLoginAt, "Login: LastLoginAt set after login");
        Console.WriteLine("  ✅ Login updates LastLoginAt timestamp");
    }
}
