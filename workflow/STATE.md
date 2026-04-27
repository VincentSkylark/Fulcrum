# Project State

## Current Position

- **Active Phase:** Phase 4: News Ingestion (Not Started)
- **Last Completed:** Phase 3: Core Abstractions + AI Orchestration (2026-04-27)
- **Date:** 2026-04-27

## Completed Work

### Phase 1: Foundation (2026-04-13)

- Solution file (.slnx) + 11 projects created
- Directory.Packages.props for central package management
- Fulcrum.AppHost + .NET Aspire 13 for local dev orchestration
- PostgreSQL via Aspire (app-db)
- OpenAPI/Swagger
- Global exception handler + Result<T> error taxonomy
- Health check endpoints
- Explicit DI registration pattern established

### Phase 2: Authentication (2026-04-22)

- Kratos configuration + identity schema + Aspire integration (kratos-db)
- Fulcrum.Auth project with KratosClient (HttpClient over Ory SDK)
- Session middleware + CurrentUserAccessor (ICurrentUserAccessor in Core)
- Auth proxy endpoints (login, register, recovery, verification, session, account)
- Webhook endpoints (registration, login, deletion events)
- Wolverine integration event handlers in Fulcrum.API (UserRegistered, UserLoggedIn, UserDeleted)
- AppDbContext + UserProfile entity in Fulcrum.API

### Phase 3: Core Abstractions + AI Orchestration (2026-04-27)

- Domain abstractions: AggregateRoot, Entity, IDomainEvent
- Error types: Result<T>, Error, ErrorType
- Integration events: IIntegrationEvent, IEventBus, UserRegisteredEvent, UserLoggedInEvent, UserDeletedEvent
- Identity: ICurrentUserAccessor (in Core)
- Serilog structured logging with OpenTelemetry integration (ServiceDefaults)
- AI Graph Engine in Fulcrum.Core/AI/:
  - AgentState, GraphContext<TState>, INode<TState>, TaskNode<TState>
  - IEdge<TState>, DirectEdge<TState>, ConditionalEdge<TState>
  - IRouter<TState>, Graph<TState>, GraphBuilder<TState>
  - GraphExecutor with cycle protection + OpenTelemetry spans
  - GraphExecutionException

## Remaining Auth Work (deferred, can run in parallel)

- AUTH-05: Frontend auth pages (login, register, recovery)
- AUTH-06: Social sign-in (Google, Facebook, Twitter)
- AUTH-07: Account settings page
- AUTH-08: GDPR foundations (account deletion, data export endpoint)

## Decisions

| Date | Decision | Reason |
|------|----------|--------|
| 2026-04-06 | News Push Platform domain — ingest, personalize, push | Product definition: SaaS news aggregation and delivery |
| 2026-04-06 | Kratos for identity management | Avoids rebuilding auth; supports social login, sessions, GDPR |
| 2026-04-06 | PostgreSQL + pgvector | Single database for relational + vector workloads, reduces ops |
| 2026-04-06 | Redis for caching + job queues | Dual-purpose infra, team familiarity |
| 2026-04-06 | Hangfire for background jobs | .NET-native, dashboard built-in, persistent job storage |
| 2026-04-06 | Stripe for payments | Industry standard, handles compliance (PCI, SCA) |
| 2026-04-06 | Meilisearch for full-text search (evaluating) | Alternative: PostgreSQL FTS — decide during Phase 4 |
| 2026-04-06 | Push notifications deferred past alpha | Keeps alpha scope minimal; push provider decided when Notifications module is built |
| 2026-04-06 | Error tracking deferred past alpha | Structured logging via Serilog + Aspire dashboard sufficient for now |
| 2026-04-06 | Modular monolith (9 projects) | Team of 2-5; modules scale to microservices if needed |
| 2026-04-06 | Hangfire over Wolverine for background jobs | Simpler, .NET-native dashboard, team familiarity; Wolverine TBD for integration events |
| 2026-04-06 | Flat project layout (src/Fulcrum.Module) | Simpler path conventions, matches existing code |
| 2026-04-06 | Schema-per-module in shared app_db | One PostgreSQL instance, each module gets its own schema, no cross-module queries |
| 2026-04-06 | Self-defined contracts (interfaces + DTOs) for cross-module communication | Publishing module defines contract in Fulcrum.Core, consuming modules implement it |
| 2026-04-17 | .NET Aspire 13 for local dev orchestration | Replaces docker-compose with code-first infra management; service discovery built-in; Aspire dashboard for observability |
| 2026-04-18 | Custom lightweight event dispatcher (no MediatR/Wolverine) | MediatR is now commercial; Wolverine lacks .NET 10 target; Core stays dependency-free |
| 2026-04-19 | Wolverine for cross-module integration events | Outbox guarantees out of the box; eliminates custom dispatcher; supports future module extraction; referenced only in Fulcrum.API host |
| 2026-04-18 | Explicit `AddFulcrumXxx()` DI registration, no `IModule` auto-scanning | Explicit is debuggable and transparent; IEndpointGroup handles endpoint auto-discovery only |
| 2026-04-19 | Explicit endpoint registration (`MapFulcrumXxxEndpoints()`) | AOT-friendly, no source generator needed, debuggable, consistent with explicit DI philosophy |
| 2026-04-18 | `Result<T>` in Core, `IExceptionHandler` in API | Core stays HTTP-agnostic; API owns the Result→ProblemDetails mapping |
| 2026-04-22 | Kratos separate DB (`kratos-db`) on shared PostgreSQL | Same server, isolated data, one Postgres to manage |
| 2026-04-22 | Self-hosted Kratos from day one | Full control, lower cost, simpler for small team |
| 2026-04-22 | HttpClient over Ory.Client SDK for Kratos API | SDK had constructor/property mismatches; Kratos REST API is simple enough to call directly |
| 2026-04-22 | Server-driven single POST auth flows | Backend creates + submits Kratos flows; simpler for API clients |
| 2026-04-22 | No caching abstraction in Core | Modules use HybridCache directly; extract to Core only if duplication emerges |
| 2026-04-22 | No IAuditable in Core | Add when a module actually needs it |
| 2026-04-27 | Feature flags deferred past init release | No features to toggle yet; add thin passthrough in Core later if needed |

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
- **Identity:** Ory Kratos (self-hosted, v1.3.1)
- **LLM Abstractions:** Microsoft.Extensions.AI.Abstractions (IChatClient)
- **AI Orchestration:** Graph-based engine in Fulcrum.Core (nodes, edges, routers, cycle-protected execution)
- **Payments:** Stripe
- **Search:** Meilisearch (or PostgreSQL FTS)
- **Push:** TBD (post-alpha)
- **Storage:** S3-compatible — Minio (local dev), S3 (staging/prod)
- **Logging:** Serilog → structured logs + OpenTelemetry
- **CI/CD:** TBD (GitHub Actions likely)
- **Testing:** xUnit v3 + Testcontainers

## Notes

- Phases provide organization, not execution control
- TASKS.json remains the source of truth for task execution
- Each phase should be planned with `/plan` before execution begins
- Fulcrum.AppHost is the startup project (`dotnet run --project src/Fulcrum.AppHost`)
- Aspire manages PostgreSQL containers; no docker-compose needed for local dev
- ServiceDefaults project provides shared OpenTelemetry, health checks, and logging config
- Kratos container configured in AppHost with bind-mounted config from `kratos/config/`
- Fulcrum.Auth owns all Kratos communication; other modules use `ICurrentUserAccessor`
- AI Graph Engine in Fulcrum.Core/AI/ — downstream consumers: News (categorization, dedup), Recommendations (embeddings, scoring)
