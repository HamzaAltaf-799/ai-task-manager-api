using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AITaskManager.API.Configurations;
using AITaskManager.API.Interfaces;
using Microsoft.Extensions.Options;

namespace AITaskManager.API.Services;

/// <summary>
/// OpenAI-backed AI service. Activated when a valid API key is configured.
/// Calls are made via raw HttpClient — no OpenAI SDK dependency needed.
/// </summary>
public class OpenAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiService> _logger;
    private const string Endpoint = "https://api.openai.com/v1/chat/completions";

    public OpenAiService(IHttpClientFactory factory, IOptions<OpenAiSettings> settings, ILogger<OpenAiService> logger)
    {
        _http = factory.CreateClient("openai");
        _settings = settings.Value;
        _logger = logger;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public Task<string> GenerateSummaryAsync(string title, string? description) =>
        CompleteAsync($"Summarize this task in 2-3 sentences: '{title}'. Context: {description ?? "none"}. Reply with only the summary.");

    public Task<string> SuggestPriorityAsync(string title, string? description) =>
        CompleteAsync($"Reply with exactly one word (Low/Medium/High/Critical) for the priority of: '{title}'. Context: {description ?? "none"}.");

    public async Task<IEnumerable<string>> GenerateSuggestionsAsync(string title, string? description)
    {
        var raw = await CompleteAsync(
            $"Give 3 actionable productivity tips for: '{title}'. Return ONLY a JSON string array, no markdown.");
        try { return JsonSerializer.Deserialize<List<string>>(raw) ?? []; }
        catch { return [raw]; }
    }

    private async Task<string> CompleteAsync(string prompt)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = _settings.Model,
            max_tokens = _settings.MaxTokens,
            messages = new[] {
                new { role = "system", content = "You are a concise productivity assistant." },
                new { role = "user",   content = prompt }
            }
        });

        try
        {
            var resp = await _http.PostAsync(Endpoint, new StringContent(body, Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI call failed");
            throw new InvalidOperationException("AI service unavailable.", ex);
        }
    }
}

/// <summary>
/// Deterministic stub — activated when no OpenAI API key is configured.
/// Lets the entire AI feature path work in dev/CI without an external dependency.
/// </summary>
public class StubAiService : IAiService
{
    public Task<string> GenerateSummaryAsync(string title, string? description) =>
        Task.FromResult($"This task focuses on '{title}'. {description}".Trim('.') + ".");

    public Task<string> SuggestPriorityAsync(string title, string? description) =>
        Task.FromResult("Medium");

    public Task<IEnumerable<string>> GenerateSuggestionsAsync(string title, string? description) =>
        Task.FromResult<IEnumerable<string>>([
            "Break the work into small, time-boxed sub-tasks.",
            "Identify blockers upfront and resolve them before starting.",
            "Use the Pomodoro technique: 25 minutes of focused work, 5-minute break."
        ]);
}
