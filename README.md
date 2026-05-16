# AI Task Manager API

A production-grade REST API built with **ASP.NET Core 8**, demonstrating clean architecture, JWT authentication, repository pattern, cache-aside, and AI-powered task insights.

> Built as a portfolio project to showcase backend engineering across the full stack: architecture, security, testing, and DevOps.

---

## Quick Start

```bash
# Clone
git clone https://github.com/yourusername/ai-task-manager-api.git
cd ai-task-manager-api

# Run (Docker — zero local dependencies)
cp .env.example .env
# Edit .env: set JWT_SECRET
docker compose up --build

# Run (local .NET)
cd AITaskManager.API
dotnet run
# → http://localhost:5099
```

**Demo credentials (seeded on startup):**

| Role  | Email                        | Password    |
|-------|------------------------------|-------------|
| Admin | admin@aitaskmanager.dev      | Admin@123!  |
| User  | demo@aitaskmanager.dev       | Demo@123!   |

---

## Architecture

```
AITaskManager/
├── AITaskManager.API/
│   ├── Controllers/        # HTTP concerns only — validate, delegate, respond
│   ├── Services/           # Business logic (AuthService, TaskService, AiService, CacheService)
│   ├── Repositories/       # Data access (ITaskRepository, IUserRepository)
│   ├── Interfaces/         # Contracts — each layer depends on abstractions, not concretions
│   ├── Models/             # Domain entities (ApplicationUser, TaskItem)
│   ├── DTOs/               # Request/response shapes — never expose domain models directly
│   ├── Auth/               # JWT token generation, PBKDF2 password hashing, [RequireAuth] filter
│   ├── Data/               # InMemoryDatabase (swap for EF Core by reimplementing interfaces)
│   ├── Middleware/         # GlobalExceptionMiddleware, RequestLoggingMiddleware, JwtMiddleware
│   ├── Validators/         # Input validation (DataAnnotations + custom rules)
│   ├── Common/             # ApiResponse<T>, PagedResult<T>, TaskQueryParameters
│   ├── Configurations/     # Strongly-typed settings (JwtSettings, OpenAiSettings)
│   └── Extensions/         # IServiceCollection extension methods — clean Program.cs
└── AITaskManager.Tests/
    ├── Services/           # Unit tests: PasswordHasher, CacheService, TaskRepository,
    │                       #             AuthService, TaskService (72 assertions)
    ├── Integration/        # Service-stack integration: full Register→Login→CRUD→AI→Delete
    └── Helpers/            # TestData builders, custom Assert class
```

### Key Design Decisions

**Repository + Service separation** — controllers stay thin (validate input → call service → return response). Services own business rules. Repositories own queries. EF Core can replace the in-memory store by implementing `ITaskRepository` and `IUserRepository` — zero service-layer changes.

**`IAiService` abstraction** — `OpenAiService` activates when `OpenAI:ApiKey` is set; `StubAiService` is the default. CI/local dev never needs an OpenAI account. Swap providers by registering a different implementation.

**`ICacheService` abstraction** — same pattern. Currently a concurrent in-memory dictionary with TTL. Replace with Redis `IDistributedCache` by implementing the interface — no callers change.

**`ApiResponse<T>` envelope** — every endpoint returns `{ success, message, data, errors, statusCode }`. Frontends have a predictable contract regardless of operation.

**Custom JWT middleware** — `JwtMiddleware` uses `System.IdentityModel.Tokens.Jwt` (the same library that `Microsoft.AspNetCore.Authentication.JwtBearer` wraps) to validate tokens and attach `ClaimsPrincipal`. No external NuGet package needed.

**`[RequireAuth]` action filter** — equivalent to `[Authorize]`, implemented as an `IAsyncActionFilter`. Works with the custom JWT middleware and supports optional role enforcement.

---

## API Reference

### Authentication

| Method | Endpoint               | Auth   | Description               |
|--------|------------------------|--------|---------------------------|
| POST   | `/api/auth/register`   | None   | Create account, get token |
| POST   | `/api/auth/login`      | None   | Login, get token          |

### Tasks

| Method | Endpoint                      | Auth   | Description                        |
|--------|-------------------------------|--------|------------------------------------|
| GET    | `/api/tasks`                  | Bearer | List tasks (filter/sort/paginate)  |
| GET    | `/api/tasks/{id}`             | Bearer | Get single task                    |
| POST   | `/api/tasks`                  | Bearer | Create task                        |
| PUT    | `/api/tasks/{id}`             | Bearer | Partial update                     |
| DELETE | `/api/tasks/{id}`             | Bearer | Delete task                        |
| POST   | `/api/tasks/{id}/summarize`   | Bearer | AI summary + suggestions           |

### Query Parameters — `GET /api/tasks`

| Param           | Type     | Description                                                   |
|-----------------|----------|---------------------------------------------------------------|
| `page`          | int      | Page number (default: 1)                                      |
| `pageSize`      | int      | Items per page (max: 50, default: 10)                         |
| `search`        | string   | Full-text search on title and description                     |
| `status`        | enum     | `Todo`, `InProgress`, `Blocked`, `Completed`, `Archived`      |
| `priority`      | enum     | `Low`, `Medium`, `High`, `Critical`                           |
| `category`      | string   | Exact category match                                          |
| `dueBefore`     | datetime | Tasks due before this date                                    |
| `dueAfter`      | datetime | Tasks due after this date                                     |
| `sortBy`        | string   | `createdAt`, `updatedAt`, `title`, `priority`, `status`, `dueDate` |
| `sortDirection` | string   | `asc` or `desc` (default: `desc`)                             |

### Example Requests

**Register**
```http
POST /api/auth/register
Content-Type: application/json

{
  "firstName": "Jane",
  "lastName":  "Doe",
  "email":     "jane@example.com",
  "password":  "Secure@123"
}
```

**Create Task**
```http
POST /api/tasks
Authorization: Bearer <token>
Content-Type: application/json

{
  "title":       "Implement rate limiting",
  "description": "Add per-user rate limiting to prevent API abuse.",
  "priority":    "High",
  "dueDate":     "2025-12-01T00:00:00Z",
  "category":    "Security"
}
```

**Filter Tasks**
```http
GET /api/tasks?status=InProgress&priority=High&sortBy=dueDate&page=1&pageSize=10
Authorization: Bearer <token>
```

**AI Summarize**
```http
POST /api/tasks/{id}/summarize
Authorization: Bearer <token>
```
Response:
```json
{
  "success": true,
  "data": {
    "taskId": "...",
    "summary": "This task involves implementing rate limiting to prevent API abuse...",
    "suggestedPriority": "High",
    "suggestions": [
      "Break the work into small, time-boxed sub-tasks.",
      "Identify blockers upfront and resolve them before starting.",
      "Use the Pomodoro technique: 25 minutes focused, 5-minute break."
    ]
  }
}
```

---

## Environment Variables

| Variable                        | Required | Default       | Description                          |
|---------------------------------|----------|---------------|--------------------------------------|
| `JwtSettings__Secret`           | ✅       | —             | Signing key (≥32 chars)              |
| `JwtSettings__Issuer`           |          | `AITaskManager.API` | Token issuer                  |
| `JwtSettings__Audience`         |          | `AITaskManager.Clients` | Token audience            |
| `JwtSettings__ExpiryMinutes`    |          | `60`          | Token TTL                            |
| `OpenAI__ApiKey`                |          | —             | GPT key (stub used when absent)      |
| `OpenAI__Model`                 |          | `gpt-4o-mini` | Model override                       |
| `ASPNETCORE_ENVIRONMENT`        |          | `Production`  | `Development` / `Production`         |

---

## Running Tests

```bash
dotnet run --project AITaskManager.Tests
```

Output:
```
Tests: 97  ✅ Passed: 97  ❌ Failed: 0
```

Coverage spans:
- **Unit** — PasswordHasher, CacheService (TTL, prefix eviction), TaskRepository (all CRUD + security isolation), AuthService (register/login/edge cases), TaskService (cache-aside, partial update, AI summarise)
- **Integration** — full Register→Login→Create→Update→Summarize→Delete lifecycle; cross-user isolation; cache invalidation on mutation; pagination and filtering end-to-end

---

## Deployment

### Railway (recommended)

```bash
npm install -g @railway/cli
railway login
railway new
railway add postgresql  # optional — for EF Core persistence
railway up
```

Set environment variables in the Railway dashboard (see table above). The GitHub Actions workflow auto-deploys on push to `main` when `RAILWAY_TOKEN` and `RAILWAY_SERVICE_ID` secrets are configured.

### Render

1. New Web Service → connect repo
2. Build command: `dotnet publish AITaskManager.API -c Release -o publish`
3. Start command: `dotnet publish/AITaskManager.API.dll`
4. Add env vars via dashboard

### Docker (self-hosted)

```bash
docker compose up --build
# API: http://localhost:8080
# Health: http://localhost:8080/health
```

---

## Upgrading to PostgreSQL + EF Core

The repository layer is fully abstracted. To switch from the in-memory store:

1. Add packages: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
2. Create `AppDbContext : IdentityDbContext<ApplicationUser>`
3. Implement `EfTaskRepository : ITaskRepository` and `EfUserRepository : IUserRepository` using EF Core
4. Update `ServiceCollectionExtensions.AddApplicationServices()` to register the EF repos instead of the in-memory ones
5. Run `dotnet ef migrations add InitialCreate && dotnet ef database update`

Zero changes to services, controllers, or tests.

---

## Upgrading to Redis Cache

Same approach — implement `RedisCacheService : ICacheService` using `StackExchange.Redis`, register it in DI. The `TaskService` never needs to change.

---

## Future Improvements

- [ ] EF Core + PostgreSQL repositories (schema already designed in code-first style)
- [ ] Redis-backed `ICacheService`
- [ ] Refresh token rotation with Redis token store
- [ ] Email verification (SendGrid / Resend)
- [ ] Webhook notifications on status change
- [ ] Soft delete with audit trail (`DeletedAt`, `DeletedBy`)
- [ ] Role-based admin endpoints (`GET /api/admin/users`)
- [ ] OpenTelemetry + distributed tracing
- [ ] GraphQL endpoint alongside REST

---

## License

MIT
