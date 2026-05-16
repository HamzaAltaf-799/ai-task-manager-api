using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AITaskManager.API.Extensions;
using AITaskManager.API.Middleware;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddApplicationServices(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                    ?? ["http://localhost:3000"])
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("api", o =>
    {
        o.PermitLimit       = 100;
        o.Window            = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit        = 10;
    });
    opts.RejectionStatusCode = 429;
});

builder.Logging
    .ClearProviders()
    .AddConsole()
    .AddDebug();

var app = builder.Build();

// ── Pipeline ──────────────────────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<JwtMiddleware>();

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    Status    = "Healthy",
    Version   = "1.0.0",
    Timestamp = DateTime.UtcNow,
    Runtime   = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
})).AllowAnonymous();

app.Run();

public partial class Program { }
