using System.Diagnostics;

namespace AITaskManager.API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try   { await _next(context); }
        finally
        {
            sw.Stop();
            var code  = context.Response.StatusCode;
            var level = code >= 500 ? LogLevel.Error : code >= 400 ? LogLevel.Warning : LogLevel.Information;
            _logger.Log(level, "{Method} {Path} → {StatusCode} in {Ms}ms",
                context.Request.Method, context.Request.Path, code, sw.ElapsedMilliseconds);
        }
    }
}
