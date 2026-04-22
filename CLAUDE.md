# Fulcrum — Modular Monolith

## Project Context

This is a .NET 10 modular monolith with each module using its own internal architecture (VSA, Clean Architecture, or DDD — run the `architecture-advisor` skill per module if needed). The application is composed of independent modules that run in a single deployable unit (the Host) but maintain strict boundaries — each module owns its features, data, and domain logic. Modules communicate through integration events, never by direct cross-module method calls or shared database tables.

## Tech Stack

- **.NET 10** / C# 14
- **.NET Aspire 13** — Application orchestration, service discovery, and local infra management (PostgreSQL, Ory Kratos)
- **ASP.NET Core Minimal APIs** — explicit `MapFulcrumXxxEndpoints()` per module, one endpoint group per feature per module
- **Entity Framework Core** — one DbContext per module, shared PostgreSQL instance (`app-db`) with schema-per-module isolation
- **Hangfire** — background jobs, scheduled tasks, recurring fetchers
- **Wolverine** — in-process messaging with outbox guarantees for cross-module integration events
- **FluentValidation** — request validation
- **Serilog** — structured logging
- **xUnit v3** + **Testcontainers** — testing

## Architecture

Run the `architecture-advisor` skill per module to choose between VSA, Clean Architecture, or DDD. Module conventions are defined in the `project-structure`, `vertical-slice`, and `ef-core` skills.

### Module Project Layout

```
src/
├── Fulcrum.AppHost/          # .NET Aspire orchestrator (startup project)
├── Fulcrum.ServiceDefaults/  # Shared service config (OpenTelemetry, health checks)
├── Fulcrum.API/              # Host — middleware, DI wiring, MapEndpoints()
├── Fulcrum.Core/             # Framework abstractions, contracts, integration events
├── Fulcrum.Auth/             # Kratos integration, profile, session
├── Fulcrum.News/             # Ingestion, dedup, categorization, search
├── Fulcrum.Recommendations/  # Vectors, embeddings, personalization
├── Fulcrum.Billing/          # Payment, subscriptions, entitlements
├── Fulcrum.Notifications/    # Push, email, digests, delivery tracking
├── Fulcrum.Analytics/        # Engagement tracking, metrics
└── Fulcrum.Admin/            # Dashboard, moderation, source management
```

### Database Isolation

Each module owns its data through a dedicated DbContext. All modules share a single PostgreSQL instance (`app-db`) with schema-per-module isolation (e.g., `news.Articles`, `billing.Subscriptions`). No module queries another module's schema. Cross-module data flows through integration events only.

### Orchestration & Local Dev

Fulcrum uses **.NET Aspire** to manage dependencies.
- **Local Dev:** Use `dotnet run --project src/Fulcrum.AppHost` (or F5). This automatically spins up PostgreSQL (with pgAdmin) as containers. Kratos will be added when the Auth module is implemented.
- **Service Discovery:** Modules resolve Postgres via Aspire connection strings (e.g., `builder.AddNpgsqlDataSource("app-db")`), not hardcoded URLs.
- **Observability:** Access the Aspire Dashboard for unified logs and OpenTelemetry traces across all modules.
- **Service Defaults:** All services call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` from the `Fulcrum.ServiceDefaults` project, which configures OpenTelemetry, health checks, and logging in one place.

### Cross-Module Communication

Modules communicate through integration events dispatched by **Wolverine**. Event contracts (records implementing `IIntegrationEvent`) are defined in `Fulcrum.Core` by the publishing module. Consuming modules contain Wolverine message handlers that receive these events. Wolverine provides outbox guarantees, ensuring events are dispatched reliably alongside database commits — no custom dispatcher infrastructure needed.

```csharp
// Fulcrum.Core — integration event record defined by the publishing module
public sealed record ArticlePublishedEvent(
    Guid ArticleId,
    string Title,
    DateTimeOffset PublishedAt,
    Guid EventId,
    DateTimeOffset OccurredAt) : IIntegrationEvent;

// Fulcrum.Notifications — Wolverine message handler in the consuming module
public sealed class ArticlePublishedHandler(IPushService push)
{
    public async Task HandleAsync(ArticlePublishedEvent notification, CancellationToken ct)
    {
        await push.SendAsync(notification.ArticleId, notification.Title, ct);
    }
}
```

Rules:
- Event contracts are sealed records in `Fulcrum.Core` — never business logic
- Publishing module defines the event record; consuming modules define Wolverine handlers
- No direct project references between feature modules (only `Fulcrum.Core`)
- `Fulcrum.Core` has zero external NuGet dependencies — Wolverine is referenced only in `Fulcrum.API` (host)
- Wolverine provides outbox + in-process dispatch; modules use `IMessageBus` to publish

### Module Registration

Each module registers its own services and endpoints through explicit extension methods. Called explicitly in `Fulcrum.API/Program.cs`. No assembly scanning, no reflection, no source generators — explicit is debuggable, transparent, and AOT-friendly.

```csharp
// Fulcrum.API/Program.cs
builder.Services.AddFulcrumAuth();
builder.Services.AddFulcrumNews();
builder.Services.AddFulcrumBilling();

var app = builder.Build();
app.MapFulcrumAuthEndpoints();
app.MapFulcrumNewsEndpoints();
app.MapFulcrumBillingEndpoints();
```

Each module's `ServiceCollectionExtensions.cs` contains both the DI registration and endpoint mapping:

```csharp
// Fulcrum.Auth/ServiceCollectionExtensions.cs
public static IServiceCollection AddFulcrumAuth(this IServiceCollection services)
{
    services.AddScoped<ISessionService, SessionService>();
    return services;
}

public static WebApplication MapFulcrumAuthEndpoints(this WebApplication app)
{
    new LoginEndpoints().Map(app);
    new RegisterEndpoints().Map(app);
    new ProfileEndpoints().Map(app);
    return app;
}
```

Every endpoint group still gets its own file — the only change is how the host discovers them (explicit registration instead of reflection).

## Agent Routing

For any .NET implementation task, match the user's intent to the routing table below. Then read the agent definition from `dotnet-agents/<agent>.md` and its skills from `dotnet-skills/<skill-name>/SKILL.md` into your context before starting work. Do not create a subagent — load and apply the agent's instructions directly.

### Routing Table

Match user intent to agent. First match wins.

| User Intent Pattern | Agent |
|---|---|
| "set up project", "folder structure", "architecture" | dotnet-architect |
| "add module", "split into modules", "bounded context" | dotnet-architect |
| "scaffold feature", "create feature", "add feature" | dotnet-architect (+ api-designer, ef-core-specialist) |
| "init project", "setup project", "new project" | dotnet-architect |
| "choose architecture", "architecture decision" | dotnet-architect |
| "create endpoint", "API route", "OpenAPI", "swagger" | api-designer |
| "versioning", "rate limiting", "CORS" | api-designer |
| "database", "migration", "query", "DbContext", "EF" | ef-core-specialist |
| "add migration", "ef migration", "update packages" | ef-core-specialist |
| "write tests", "test strategy", "coverage" | test-engineer |
| "WebApplicationFactory", "Testcontainers", "xUnit" | test-engineer |
| "security", "authentication", "JWT", "OIDC", "authorize" | security-auditor |
| "performance", "benchmark", "memory", "profiling" | performance-analyst |
| "caching", "HybridCache", "output cache" | performance-analyst |
| "Docker", "container", "CI/CD", "pipeline", "deploy" | devops-engineer |
| "Aspire", "orchestration", "service discovery" | devops-engineer |
| "review this code", "PR review", "code quality" | code-reviewer |
| "conventions", "coding style", "detect patterns" | code-reviewer |
| "refactor" | code-reviewer (+ dotnet-architect) |
| "build errors", "fix build", "won't compile" | build-error-resolver |
| "clean up", "dead code", "unused code" | refactor-cleaner |

### Skill Maps

Read `modern-csharp` first for every agent, then load the domain-specific skills listed below.

| Agent | Skills |
|---|---|
| dotnet-architect | architecture-advisor, project-structure, scaffolding, project-setup + conditional: vertical-slice, clean-architecture, ddd |
| api-designer | minimal-api, api-versioning, authentication, error-handling |
| ef-core-specialist | ef-core, configuration, migration-workflow |
| test-engineer | testing |
| security-auditor | authentication, configuration |
| performance-analyst | caching |
| devops-engineer | docker, ci-cd, aspire |
| code-reviewer | code-review-workflow, convention-learner + contextual: loads relevant skills based on files under review |
| build-error-resolver | autonomous-loops + contextual: ef-core, dependency-injection |
| refactor-cleaner | de-sloppify + contextual: testing, ef-core |

## Commands

```bash
# Build entire solution
dotnet build

# Run the host (development)
dotnet run --project src/Fulcrum.AppHost

# Run all tests
dotnet test

# Run tests for a specific module
dotnet test tests/Fulcrum.[Module].Tests

# Add EF migration for a specific module
dotnet ef migrations add [Name] \
  --project src/Fulcrum.[Module] \
  --startup-project src/Fulcrum.AppHost \
  --context [Module]DbContext

# Apply migrations for a specific module
dotnet ef database update \
  --project src/Fulcrum.[Module] \
  --startup-project src/Fulcrum.API \
  --context [Module]DbContext

# Format check
dotnet format --verify-no-changes

# Task management
python3 agent/tasks.py add -t "Title" -d "Description" -T backend
python3 agent/tasks.py list
python3 agent/tasks.py next
python3 agent/tasks.py status ID completed --commit "hash" --note "note"
python3 agent/tasks.py status ID failed --branch "task-1-slug" --note "reason"

# Autonomous execution
python3 agent/task-runner.py --dry-run
python3 agent/task-runner.py --max-tasks 5
```

## Branching Strategy

Each task runs on its own branch:
- Branch name: `task-{id}-{slug}`
- Only merge to `main` after `/validate` passes
- Failed tasks keep branch for human review

## Key Files

| File | Purpose |
|------|---------|
| `TASKS.json` | Source of truth for task execution |
| `workflow/STATE.md` | Current project position |
| `workflow/ROADMAP.md` | Phases and requirements |
| `plans/*.md` | Task implementation details |

## Workflow

- **Plan first** — Enter plan mode for any non-trivial task (3+ steps or architecture decisions). Iterate until the plan is solid before writing code.
- **Verify before done** — Run `dotnet build` and `dotnet test` after changes. Use `get_diagnostics` via MCP to catch warnings. Ask: "Would a staff engineer approve this?"
- **Fix bugs autonomously** — When given a bug report, investigate and fix it without hand-holding. Check logs, errors, failing tests — then resolve them.
- **Stop and re-plan** — If implementation goes sideways, STOP and re-plan. Don't push through a broken approach.
- **Use subagents** — Offload research, exploration, and parallel analysis to subagents. One task per subagent for focused execution.
- **Learn from corrections** — After any correction, capture the pattern in memory so the same mistake never recurs.

