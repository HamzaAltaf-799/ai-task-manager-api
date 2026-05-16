using AITaskManager.Tests.Integration;
using AITaskManager.Tests.Services;
using AITaskManager.Tests.Helpers;

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║   AI Task Manager — Test Suite           ║");
Console.WriteLine("╚══════════════════════════════════════════╝");

// ── Unit Tests ────────────────────────────────────────────────────────────────
PasswordHasherTests.RunAll();
CacheServiceTests.RunAll();
TaskRepositoryTests.RunAll();
AuthServiceTests.RunAll();
TaskServiceTests.RunAll();

// ── Integration Tests ─────────────────────────────────────────────────────────
await ApiIntegrationTests.RunAllAsync();

Assert.Summary();
