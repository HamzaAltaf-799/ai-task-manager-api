using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AITaskManager.API.Auth;
using AITaskManager.API.Interfaces;

namespace AITaskManager.API.Middleware;

/// <summary>
/// Validates JWT from Authorization header and attaches ClaimsPrincipal to the request.
/// Replaces the external Microsoft.AspNetCore.Authentication.JwtBearer package using
/// the same underlying Microsoft.IdentityModel.Tokens library that package wraps.
/// </summary>
public class JwtMiddleware
{
    private readonly RequestDelegate _next;

    public JwtMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, JwtTokenService tokenService, IUserRepository users)
    {
        var token = ExtractToken(context);
        if (token is not null)
            await AttachUserAsync(context, token, tokenService, users);

        await _next(context);
    }

    private static string? ExtractToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.FirstOrDefault();
        return header?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? header["Bearer ".Length..]
            : null;
    }

    private static async Task AttachUserAsync(
        HttpContext context, string token,
        JwtTokenService tokenService, IUserRepository users)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, tokenService.GetValidationParameters(), out _);
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId is not null)
            {
                var user = await users.GetByIdAsync(userId);
                if (user is { IsActive: true })
                {
                    context.User = principal;
                    context.Items["CurrentUser"] = user;
                }
            }
        }
        catch
        {
            // Invalid token — request continues without identity.
            // [Authorize] endpoints will reject it at the authorization step.
        }
    }
}
