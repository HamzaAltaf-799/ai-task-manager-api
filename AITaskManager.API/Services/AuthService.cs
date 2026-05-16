using AITaskManager.API.Auth;
using AITaskManager.API.DTOs.Auth;
using AITaskManager.API.Interfaces;
using AITaskManager.API.Models;

namespace AITaskManager.API.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly JwtTokenService _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUserRepository users, JwtTokenService jwt, ILogger<AuthService> logger)
    {
        _users  = users;
        _jwt    = jwt;
        _logger = logger;
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await _users.EmailExistsAsync(request.Email))
        {
            _logger.LogWarning("Registration attempt for existing email: {Email}", request.Email);
            return null;
        }

        if (!IsPasswordStrong(request.Password, out var reason))
        {
            _logger.LogWarning("Weak password on registration for {Email}: {Reason}", request.Email, reason);
            return null;
        }

        var user = new ApplicationUser
        {
            FirstName    = request.FirstName.Trim(),
            LastName     = request.LastName.Trim(),
            Email        = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role         = "User"
        };

        await _users.CreateAsync(user);
        _logger.LogInformation("User registered: {Email}", user.Email);

        return BuildResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _users.GetByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Login attempt for unknown/inactive account: {Email}", request.Email);
            return null;
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login (wrong password) for: {Email}", request.Email);
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        _logger.LogInformation("User logged in: {Email}", user.Email);
        return BuildResponse(user);
    }

    private AuthResponse BuildResponse(ApplicationUser user)
    {
        var token = _jwt.GenerateToken(user);
        return new AuthResponse(
            AccessToken: token,
            TokenType: "Bearer",
            ExpiresIn: 3600,
            User: new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.FullName, user.Role, user.CreatedAt)
        );
    }

    private static bool IsPasswordStrong(string password, out string reason)
    {
        if (password.Length < 8)               { reason = "Minimum 8 characters.";           return false; }
        if (!password.Any(char.IsUpper))       { reason = "Must contain uppercase letter.";   return false; }
        if (!password.Any(char.IsLower))       { reason = "Must contain lowercase letter.";   return false; }
        if (!password.Any(char.IsDigit))       { reason = "Must contain a digit.";            return false; }
        if (!password.Any(c => !char.IsLetterOrDigit(c))) { reason = "Must contain special character."; return false; }
        reason = string.Empty;
        return true;
    }
}
