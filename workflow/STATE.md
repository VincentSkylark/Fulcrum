# Project State

## Current Position

- **Active Phase:** Phase 1: Foundation (Completed)
- **Last Completed:** Phase 1: Foundation (2026-04-13) + Fulcrum.Core implementation
- **Date:** 2026-04-18

## Current Phase Tasks

- **Task 1:** Create class library projects ~~Completed 2026-04-13~~
- **Task 2:** Register projects in Fulcrum.slnx ~~Completed 2026-04-13~~
- **Task 3:** Wire up project references ~~Completed 2026-04-13~~
- **Task 4:** Set up central package management ~~Completed 2026-04-13~~
- **Task 5:** Clean up Fulcrum.API Program.cs ~~Completed 2026-04-13~~

## Decisions

| Date | Decision | Reason |
|------|----------|--------|
| 2026-04-06 | News Push Platform domain — ingest, personalize, push | Product definition: SaaS news aggregation and delivery |
| 2026-04-06 | Kratos for identity management | Avoids rebuilding auth; supports social login, sessions, GDPR |
| 2026-04-06 | PostgreSQL + pgvector | Single database for relational + vector workloads, reduces ops |
| 2026-04-06 | Redis for caching + job queues | Dual-purpose infra, team familiarity |
| 2026-04-06 | Hangfire for background jobs | .NET-native, dashboard built-in, persistent job storage |
| 2026-04-06 | Stripe for payments | Industry standard, handles compliance (PCI, SCA) |
| 2026-04-06 | Meilisearch for full-text search (evaluating) | Alternative: PostgreSQL FTS — decide during Phase 3 |
| 2026-04-06 | Push notifications deferred past alpha | Keeps alpha scope minimal; push provider (FCM or other) decided when Notifications module is built |
| 2026-04-06 | Error tracking deferred past alpha | Keeps alpha scope minimal; structured logging via Serilog + Aspire dashboard sufficient for now |
| 2026-04-06 | Modular monolith (9 projects) | Team of 2-5; modules scale to microservices if needed |
| 2026-04-06 | Hangfire over Wolverine for background jobs | Simpler, .NET-native dashboard, team familiarity; Wolverine TBD for integration events |
| 2026-04-06 | Flat project layout (src/Fulcrum.Module) | Simpler path conventions, matches existing code |
| 2026-04-06 | Schema-per-module in shared app_db | One PostgreSQL instance, each module gets its own schema, no cross-module queries |
| 2026-04-06 | Self-defined contracts (interfaces + DTOs) for cross-module communication | No generic mediator commands; publishing module defines contract in Fulcrum.Core, consuming modules implement it |
| 2026-04-17 | .NET Aspire 13 for local dev orchestration | Replaces docker-compose with code-first infra management; service discovery built-in; Aspire dashboard for observability |
| 2026-04-18 | Custom lightweight event dispatcher (no MediatR/Wolverine) | MediatR is now commercial (license key required); Wolverine lacks .NET 10 target; Core stays dependency-free; Wolverine re-evaluated in Phase 3 if outbox guarantees needed |
| 2026-04-19 | Wolverine for cross-module integration events | Provides outbox guarantees out of the box; eliminates custom dispatcher; supports future module extraction to separate services; Wolverine referenced only in Fulcrum.API host |
| 2026-04-18 | Explicit `AddFulcrumXxx()` DI registration, no `IModule` auto-scanning | Explicit is debuggable and transparent; IEndpointGroup handles endpoint auto-discovery only; no hidden assembly scanning for DI |
| 2026-04-19 | Explicit endpoint registration (`MapFulcrumXxxEndpoints()`) instead of reflection-based auto-discovery | AOT-friendly, no source generator needed, debuggable, consistent with explicit DI philosophy; `IEndpointGroup` and `EndpointDiscovery` removed from Core |
| 2026-04-18 | `Result<T>` in Core, `IExceptionHandler` in API | Core stays HTTP-agnostic; API owns the Result→ProblemDetails mapping; modules use Result<T> in any context (HTTP, jobs, CLI) |

## Blockers

- None

## Technology Stack

- **Runtime:** .NET 10 / C# 14
- **Orchestration:** .NET Aspire 13 (AppHost + ServiceDefaults)
- **API:** ASP.NET Core Minimal APIs + explicit `MapFulcrumXxxEndpoints()` per module
- **ORM:** Entity Framework Core (one DbContext per module)
- **Database:** PostgreSQL (app-db + kratos-db) + pgvector
- **Cache / Queue:** Redis
- **Background Jobs:** Hangfire
- **Inter-Module Events:** Wolverine (outbox guarantees, in-process dispatch, `IMessageBus`)
- **Identity:** Ory Kratos
- **Payments:** Stripe
- **Search:** Meilisearch (or PostgreSQL FTS)
- **Push:** TBD (post-alpha)
- **Storage:** S3-compatible — Minio (local dev), S3 (staging/prod)
- **Logging:** Serilog → structured logs
- **CI/CD:** TBD (GitHub Actions likely)
- **Testing:** xUnit v3 + Testcontainers

## Notes

- Phases provide organization, not execution control
- TASKS.json remains the source of truth for task execution
- Each phase should be planned with `/plan` before execution begins
- Fulcrum.AppHost is the startup project (`dotnet run --project src/Fulcrum.AppHost`)
- Aspire manages PostgreSQL containers; no docker-compose needed for local dev
- ServiceDefaults project provides shared OpenTelemetry, health checks, and logging config
- Kratos container configuration deferred to Phase 2 (Auth) — `kratos/` config directory is empty
- Fulcrum.API has clean minimal host with Aspire service defaults wired in
