using System.Security.Claims;
using AITaskManager.API.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AITaskManager.API.Auth;

/// <summary>
/// Action filter that requires an authenticated user.
/// Equivalent to [Authorize] — works with our custom JWT middleware.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAuthAttribute : Attribute, IAsyncActionFilter
{
    private readonly string? _requiredRole;

    public RequireAuthAttribute(string? role = null) => _requiredRole = role;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = UnauthorizedResult("Authentication required.");
            return;
        }

        if (_requiredRole is not null)
        {
            var userRole = user.FindFirstValue(ClaimTypes.Role);
            if (!string.Equals(userRole, _requiredRole, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = ForbiddenResult("Insufficient permissions.");
                return;
            }
        }

        await next();
    }

    private static ObjectResult UnauthorizedResult(string msg) =>
        new(ApiResponse<object>.Fail(msg, 401)) { StatusCode = 401 };

    private static ObjectResult ForbiddenResult(string msg) =>
        new(ApiResponse<object>.Fail(msg, 403)) { StatusCode = 403 };
}
