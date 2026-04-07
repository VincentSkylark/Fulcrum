# Fulcrum — Modular Monolith

## Project Context

This is a .NET 10 modular monolith with each module using its own internal architecture (VSA, Clean Architecture, or DDD — run the `architecture-advisor` skill per module if needed). The application is composed of independent modules that run in a single deployable unit (the Host) but maintain strict boundaries — each module owns its features, data, and domain logic. Modules communicate through integration events, never by direct cross-module method calls or shared database tables.

## Tech Stack

- **.NET 10** / C# 14
- **ASP.NET Core Minimal APIs** — `IEndpointGroup` per feature with `app.MapEndpoints()` auto-discovery, one endpoint group per feature per module
- **Entity Framework Core** — one DbContext per module, shared PostgreSQL instance (`app_db`) with schema-per-module isolation
- **Hangfire** — background jobs, scheduled tasks, recurring fetchers
- **TBD** — inter-module messaging via integration events (evaluating Wolverine, MediatR, custom)
- **FluentValidation** — request validation
- **Serilog** — structured logging
- **xUnit v3** + **Testcontainers** — testing

## Architecture

Run the `architecture-advisor` skill per module to choose between VSA, Clean Architecture, or DDD. Module conventions are defined in the `project-structure`, `vertical-slice`, and `ef-core` skills.

### Module Project Layout

```
src/
├── Fulcrum.API/              # Host — middleware, DI wiring, MapEndpoints()
├── Fulcrum.Auth/             # Kratos integration, profile, session
├── Fulcrum.News/             # Ingestion, dedup, categorization, search
├── Fulcrum.Recommendations/  # Vectors, embeddings, personalization
├── Fulcrum.Billing/          # Payment, subscriptions, entitlements
├── Fulcrum.Notifications/    # Push, email, digests, delivery tracking
├── Fulcrum.Analytics/        # Engagement tracking, metrics
├── Fulcrum.Admin/            # Dashboard, moderation, source management
└── Fulcrum.Shared/           # Contracts, integration events, shared utilities
```

### Database Isolation

Each module owns its data through a dedicated DbContext. All modules share a single PostgreSQL instance (`app_db`) with schema-per-module isolation (e.g., `news.Articles`, `billing.Subscriptions`). No module queries another module's schema. Cross-module data flows through integration events only.

### Cross-Module Communication

Modules communicate through self-defined contracts (interfaces + DTOs) in `Fulcrum.Shared`. No generic mediator commands or shared service locator patterns. Each module defines the events it publishes and the handlers it expects as explicit contracts.

```csharp
// Fulcrum.Shared — contract defined by the publishing module
public interface IArticlePublishedEvent
{
    Guid ArticleId { get; }
    string Title { get; }
    DateTimeOffset PublishedAt { get; }
}

// Fulcrum.Notifications — handler in the consuming module
public sealed class SendPushOnArticlePublished(IPushService push) : IArticlePublishedEvent
{
    // implementation
}
```

Rules:
- Contracts are interfaces + DTOs only — never business logic
- Publishing module defines the contract; consuming modules implement it
- No direct project references between feature modules (only `Fulcrum.Shared`)
- Concrete event dispatch mechanism (in-process bus, Wolverine, etc.) TBD

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
dotnet run --project src/Fulcrum.API

# Run all tests
dotnet test

# Run tests for a specific module
dotnet test tests/Fulcrum.[Module].Tests

# Add EF migration for a specific module
dotnet ef migrations add [Name] \
  --project src/Fulcrum.[Module] \
  --startup-project src/Fulcrum.API \
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

