# Fulcrum — News Push Platform Roadmap

## Overview

Fulcrum is a SaaS news push platform that ingests news from multiple sources, personalizes content using AI-powered recommendations, and delivers push notifications across channels. Built as a .NET 10 modular monolith.

**Team size:** 2-5 developers

## Solution Structure

```
src/
├── Fulcrum.API/              # API host, middleware, DI wiring
├── Fulcrum.Core/             # Framework abstractions, contracts, integration events
├── Fulcrum.Auth/             # Kratos integration, profile, session
├── Fulcrum.News/             # Ingestion, dedup, categorization, search, storage
├── Fulcrum.Recommendations/  # Vectors, embeddings, personalization
├── Fulcrum.Billing/          # Payment, subscriptions, entitlements
├── Fulcrum.Notifications/    # Push, email, digests, delivery tracking
├── Fulcrum.Analytics/        # Engagement tracking, metrics
└── Fulcrum.Admin/            # Dashboard, moderation, source management
```

## Infrastructure (Docker Compose — dev)

```
services:
  app:             # .NET monolith
  kratos:          # identity
  kratos-migrate:
  db:              # PostgreSQL + pgvector
  redis:           # caching + job queues
  meilisearch:     # full-text search (if not using PG FTS)
  mailslurper:     # catch emails locally
  minio:           # S3-compatible local object storage
```

---

## Phase 1: Foundation

- **Goal:** Establish solution structure, infrastructure, and developer experience baseline
- **Status:** Not Started
- **Requirements:**
  - FND-01: Solution file (.slnx) + project structure with all 9 projects (Core replaces Shared)
  - FND-02: `Directory.Packages.props` for central package management
  - FND-03: Dockerfile + docker-compose.yml for local development
  - FND-04: PostgreSQL setup (app_db + kratos_db)
  - FND-05: Redis for caching and background job queues
  - FND-06: Serilog structured logging to console + file
  - FND-07: Sentry error tracking integration
  - FND-08: CI/CD pipeline (build, test, deploy)
  - FND-09: OpenAPI/Swagger documentation
  - FND-10: `IEndpointGroup` auto-discovery wired in Program.cs
  - FND-11: Global exception handler (`IExceptionHandler`)
  - FND-12: Health check endpoints
- **Tasks:** TBD

---

## Phase 2: Authentication

- **Goal:** User identity, sessions, social login, and GDPR foundations
- **Status:** Not Started
- **Requirements:**
  - AUTH-01: Kratos configuration + identity schema
  - AUTH-02: Session middleware + CurrentUser context
  - AUTH-03: Profile entity + webhooks (registration, settings, deletion)
  - AUTH-04: Auth flow proxy endpoints (login, register, recovery, verification)
  - AUTH-05: Frontend auth pages (login, register, recovery)
  - AUTH-06: Social sign-in (Google, Facebook, Twitter)
  - AUTH-07: Account settings page
  - AUTH-08: GDPR foundations (account deletion, data export endpoint)
- **Tasks:** TBD

---

## Phase 3: Core Domain — News Ingestion

- **Goal:** End-to-end news pipeline from source to searchable storage
- **Status:** Not Started
- **Requirements:**
  - NEWS-01: News source entity (RSS, APIs, scrapers)
  - NEWS-02: Background job framework (Hangfire)
  - NEWS-03: News fetcher jobs (pull from sources on schedule)
  - NEWS-04: Metadata extraction + preprocessing pipeline
  - NEWS-05: Content deduplication (similarity detection)
  - NEWS-06: Content categorization / tagging (sports, tech, etc.)
  - NEWS-07: News article entity + CRUD API
  - NEWS-08: Full-text search (PostgreSQL FTS or Meilisearch)
  - NEWS-09: Media storage (S3 + CDN for thumbnails/images)
- **Tasks:** TBD

---

## Phase 4: Personalization

- **Goal:** AI-powered recommendations and personalized feeds
- **Status:** Not Started
- **Requirements:**
  - REC-01: User preference / interest entity + UI
  - REC-02: Vector DB integration (pgvector or Qdrant)
  - REC-03: Embedding generation (news articles → vectors)
  - REC-04: Recommendation engine (vector similarity + category boost)
  - REC-05: Personalized feed API endpoint
  - REC-06: Analytics — engagement tracking (opens, clicks, scroll depth)
- **Tasks:** TBD

---

## Phase 5: Payment

- **Goal:** Monetization through subscriptions with Stripe
- **Status:** Not Started
- **Requirements:**
  - PAY-01: Plan / tier entity
  - PAY-02: Stripe integration
  - PAY-03: Subscription lifecycle (create, upgrade, cancel, renew)
  - PAY-04: Entitlement checks (middleware — free vs paid features)
  - PAY-05: Billing webhooks (payment success, failure, chargeback)
  - PAY-06: Transactional email — billing receipts
- **Tasks:** TBD

---

## Phase 6: Push Delivery

- **Goal:** Multi-channel notification delivery and tracking
- **Status:** Not Started
- **Requirements:**
  - PUSH-01: Push infrastructure (FCM for mobile + web push)
  - PUSH-02: User notification preferences (topics, frequency, quiet hours)
  - PUSH-03: Real-time push — breaking news
  - PUSH-04: Scheduled digests (daily/weekly email + push summary)
  - PUSH-05: Delivery tracking (sent, delivered, opened, failed)
  - PUSH-06: Transactional email — welcome, digest, re-engagement
- **Tasks:** TBD

---

## Phase 7: Admin

- **Goal:** Administrative tools for content and user management
- **Status:** Not Started
- **Requirements:**
  - ADM-01: Admin dashboard (user management, metrics overview)
  - ADM-02: Content moderation tools (flag, hide, review queue)
  - ADM-03: News source management (add, disable, health status)
  - ADM-04: Job monitoring (failed jobs, queue depth)
  - ADM-05: Analytics dashboard (engagement, revenue, growth)
- **Tasks:** TBD

---

## Phase 8: API + Performance

- **Goal:** Production-grade API hardening and performance optimization
- **Status:** Not Started
- **Requirements:**
  - PERF-01: Rate limiting (per user, per tier)
  - PERF-02: Redis caching for hot paths (feeds, recommendations)
  - PERF-03: API versioning strategy
  - PERF-04: Pagination (cursor-based feeds)
  - PERF-05: Abuse prevention (bot signup detection, scrape protection)
- **Tasks:** TBD

---

## Phase 9: Production

- **Goal:** Production deployment, monitoring, compliance, and localization
- **Status:** Not Started
- **Requirements:**
  - PROD-01: docker-compose.prod.yml
  - PROD-02: Deploy script + secret manager
  - PROD-03: Reverse proxy (Caddy/Nginx) + HTTPS + CDN
  - PROD-04: Monitoring + alerting (Prometheus/Grafana or managed)
  - PROD-05: Backup strategy (DB + media)
  - PROD-06: GDPR finalization (consent management, cookie policy, DPA)
  - PROD-07: Localization / i18n (multi-language UI)
- **Tasks:** TBD

---

## Deferred (Not Day One)

| Area | When It Matters | Complexity |
|------|----------------|------------|
| Feature flags | A/B testing recommendation algorithms or gradual feature rollout | Low |
| Webhook API for integrations | Customers want to pipe news into Slack, Discord, Zapier | Medium |
| Audit logging | "Who changed what, when" for debugging or compliance | Low |
| Multi-tenancy | Only if pivoting to B2B (companies as customers) | High |
| SSO / SAML | B2B enterprise customers require it — Kratos supports it | Medium |
| Public API + API keys | Third-party developers building on the platform | Medium |
| Content expiry / archival | Storage cost management — auto-archive or delete old news | Low |
| Offline support / PWA | Mobile web users need offline reading | Medium |
| A/B testing framework | Testing pricing pages, onboarding flows, UI variants | Medium |
| Read-later / bookmarks | Users save articles — touches feed UX | Low |
| Social features | Comments, sharing, following — scope creep risk | High |
| Custom RSS output | Users export personalized feed as RSS — sticky feature | Low |
| Compliance reporting | SOC 2 for enterprise customers | High |
| Multi-region deployment | Latency optimization for international users | High |

---

## Phase Template

```markdown
## Phase N: [Phase Name]

- **Goal:** [What we're building]
- **Status:** Not Started | In Progress | Completed
- **Requirements:**
  - [ID-01]: [Requirement description]
- **Tasks:** Link to TASKS.json IDs
```
