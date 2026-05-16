using System.Net;
using System.Text.Json;
using AITaskManager.API.Common;

namespace AITaskManager.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next   = next;
        _logger = logger;
        _env    = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex) { await HandleAsync(context, ex); }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (code, message) = ex switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized."),
            KeyNotFoundException        => (HttpStatusCode.NotFound,     ex.Message),
            ArgumentException           => (HttpStatusCode.BadRequest,   ex.Message),
            InvalidOperationException   => (HttpStatusCode.BadRequest,   ex.Message),
            _                           => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        _logger.LogError(ex, "Unhandled {ExType} on {Method} {Path}",
            ex.GetType().Name, context.Request.Method, context.Request.Path);

        var response = ApiResponse<object>.Fail(
            message: message,
            statusCode: (int)code,
            errors: _env.IsDevelopment() ? [ex.ToString()] : null);

        context.Response.StatusCode  = (int)code;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
