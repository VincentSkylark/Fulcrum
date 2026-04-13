# Project State

## Current Position

- **Active Phase:** Phase 1: Foundation
- **Last Completed:** None
- **Date:** 2026-04-06

## Current Phase Tasks

- **Task 1:** Create class library projects
  - Description: 7 new classlib projects (Core, News, Recommendations, Billing, Notifications, Analytics, Admin)
  - Status: Pending
- **Task 2:** Register projects in Fulcrum.slnx
  - Description: Add 7 new projects to existing .slnx
  - Status: Pending
  - Depends on: Task 1
- **Task 3:** Wire up project references
  - Description: Module → Core; API → all modules + Core; no cross-module refs
  - Status: Pending
  - Depends on: Task 2
- **Task 4:** Set up central package management
  - Description: Create Directory.Packages.props, migrate versions
  - Status: Pending
  - Depends on: Task 1
- **Task 5:** Clean up Fulcrum.API Program.cs
  - Description: Remove weather forecast boilerplate
  - Status: Pending
  - Depends on: Task 2

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
| 2026-04-06 | FCM for push notifications | Covers mobile (Android/iOS) + web push via single provider |
| 2026-04-06 | Sentry for error tracking | Structured error reporting with context |
| 2026-04-06 | Modular monolith (9 projects) | Team of 2-5; modules scale to microservices if needed |
| 2026-04-06 | Hangfire over Wolverine for background jobs | Simpler, .NET-native dashboard, team familiarity; Wolverine TBD for integration events |
| 2026-04-06 | Flat project layout (src/Fulcrum.Module) | Simpler path conventions, matches existing code |
| 2026-04-06 | Schema-per-module in shared app_db | One PostgreSQL instance, each module gets its own schema, no cross-module queries |
| 2026-04-06 | Self-defined contracts (interfaces + DTOs) for cross-module communication | No generic mediator commands; publishing module defines contract in Fulcrum.Shared, consuming modules implement it |

## Blockers

- None

## Technology Stack

- **Runtime:** .NET 10 / C# 14
- **API:** ASP.NET Core Minimal APIs + IEndpointGroup auto-discovery
- **ORM:** Entity Framework Core (one DbContext per module)
- **Database:** PostgreSQL (app_db + kratos_db) + pgvector
- **Cache / Queue:** Redis
- **Background Jobs:** Hangfire
- **Inter-Module Events:** TBD (Wolverine, MediatR, or custom — decision needed before Phase 3)
- **Identity:** Ory Kratos
- **Payments:** Stripe
- **Search:** Meilisearch (or PostgreSQL FTS)
- **Push:** Firebase Cloud Messaging (FCM)
- **Storage:** S3-compatible (Minio local, cloud S3 prod)
- **Logging:** Serilog → structured logs
- **Errors:** Sentry
- **CI/CD:** TBD (GitHub Actions likely)
- **Testing:** xUnit v3 + Testcontainers

## Notes

- Phases provide organization, not execution control
- TASKS.json remains the source of truth for task execution
- Each phase should be planned with `/plan` before execution begins
- Fulcrum.Auth project exists but is scaffolding only (placeholder Class1.cs)
- Fulcrum.API has default template code — needs Foundation phase work
