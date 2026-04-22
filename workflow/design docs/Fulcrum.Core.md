# Fulcrum.Core — Design Doc

## Context

Fulcrum.Core is the shared kernel for a .NET 10 modular monolith. It contains the contracts, primitives, and plumbing that **two or more modules need to share**. Currently empty — this doc defines what goes in before Phase 2 (Auth) begins.

Core principles:
- **Contracts only, never business logic** — interfaces, DTOs, base types
- **HTTP-agnostic** — Core doesn't know about status codes or ProblemDetails

---

## 1. Integration Event Contracts (Cross-Module Communication)

Cross-module events are dispatched by **Wolverine**, which provides outbox guarantees and reliable in-process messaging. Event contracts (records implementing `IIntegrationEvent`) are defined in Core by the publishing module. Wolverine is referenced only in `Fulcrum.API` (the host) — Core stays dependency-free.

### What goes in Core

```
Events/
  IIntegrationEvent.cs        # Marker interface (EventId, OccurredAt)
```

Integration events are sealed records implementing the marker interface. Example:

```csharp
// Defined by the publishing module (News) in Core
public sealed record ArticlePublishedEvent(
    Guid ArticleId,
    string Title,
    DateTimeOffset PublishedAt,
    Guid EventId,
    DateTimeOffset OccurredAt) : IIntegrationEvent;

// Consuming modules (Notifications, Recommendations) define
// Wolverine message handlers in their own project
```

### What Wolverine handles (not in Core)
- Outbox guarantees (events dispatched alongside DB commits)
- Message handler discovery and invocation
- `IMessageBus` for publishing events from application services

### What stays out
- No MediatR `INotification` dependency (commercial license + couples Core to framework)
- No Wolverine dependency in Core — Wolverine is only in the API host project
- No custom dispatcher infrastructure

---

## 2. Result Pattern (Error Taxonomy)

Core defines the **taxonomy** — what errors look like. API defines the **HTTP mapping**.

### What goes in Core

```
Errors/
  Result.cs          # Result and Result<T> (Success/Failure)
  Error.cs           # Immutable error record (Type, Code, Message)
  ErrorType.cs       # enum: NotFound, Validation, Conflict, Unauthorized, Unexpected
```

```csharp
public sealed record Error(ErrorType Type, string Code, string Message);

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);
}
```

### What goes in Fulcrum.API (not Core)

```
Infrastructure/
  GlobalExceptionHandler.cs   # IExceptionHandler → Result/Error → ProblemDetails
  ResultHttpExtensions.cs     # Result<T>.ToHttpResult() extension methods
```

### Why the split
Core stays HTTP-agnostic so modules can use `Result<T>` in background jobs, CLI tools, or any non-HTTP context. API owns the single place where `ErrorType.NotFound` becomes HTTP 404.

---

## 3. Identity Context

Auth module owns Kratos logic. Core owns only the **mechanism** for other modules to access current user identity.

### What goes in Core

```
Identity/
  ICurrentUserAccessor.cs     # interface: UserId, Email, Tier, IsAuthenticated
```

```csharp
public interface ICurrentUserAccessor
{
    Guid UserId { get; }
    string Email { get; }
    string Tier { get; }
    bool IsAuthenticated { get; }
}
```

### What stays out
- **No permission constants** in Core — `Permissions.News.Publish` belongs in the News module
- **No policy definitions** in Core — each module defines its own authorization policies
- Core provides the mechanism (who is the user), modules provide the rules (what can they do)
- If a shared authorization attribute is needed later, Core can have a `[HasPermission]` attribute that modules populate with their own constants

---

## 4. Domain Building Blocks

Common base types that keep modules consistent.

### What goes in Core

```
Domain/
  Entity.cs                # Base entity with Id + equality
  AggregateRoot.cs         # Extends Entity with domain event collection

  IDomainEvent.cs          # Marker interface for in-module domain events
```

### What stays out
- **No pre-created value objects** — only add `Money`, `EmailAddress`, etc. when 2+ modules actually need them
- **No repository interfaces** — modules inject `DbContext` directly (architecture rule: "DbContext is already a Unit of Work + Repository")
- **No Unit of Work abstractions** — EF Core `DbContext` already is UoW

---

## 5. Module Registration (NOT in Core)

Each module owns its own `ServiceCollectionExtensions` with explicit `AddFulcrumXxx()` and `MapFulcrumXxxEndpoints()` extension methods. Called explicitly in `Fulcrum.API/Program.cs`. No assembly scanning, no reflection, no source generators — explicit is debuggable, transparent, and AOT-friendly.

```csharp
// Program.cs — explicit, debuggable, no magic
builder.Services.AddFulcrumAuth();
builder.Services.AddFulcrumNews();

var app = builder.Build();
app.MapFulcrumAuthEndpoints();
app.MapFulcrumNewsEndpoints();
```

### What stays out of Core
- **No `IEndpointGroup` interface** — modules define their own `*Endpoints.cs` classes with a `Map(IEndpointRouteBuilder)` method
- **No `EndpointDiscovery` / `MapEndpoints()`** — no reflection-based auto-wiring
- **No `IModule` auto-scanning** — each module is registered explicitly in Program.cs

---

## 6. Domain Logging Contracts

All modules need structured, consistent logging for domain events and operations. Core defines the contract — modules call it, the implementation writes wherever it needs to (Serilog, database, external sink).

### What goes in Core

```
Logging/
  IDomainLogger.cs           # Interface for structured domain logging
  DomainLogEntry.cs          # Structured log DTO (Module, Action, EntityType, EntityId, Details, UserId, Timestamp)
```

```csharp
public sealed record DomainLogEntry(
    string Module,
    string Action,
    string? EntityType,
    string? EntityId,
    string? Details,
    Guid? UserId,
    DateTimeOffset Timestamp);

public interface IDomainLogger
{
    Task LogAsync(DomainLogEntry entry, CancellationToken ct = default);
}
```

### What stays out
- **No Serilog / OpenTelemetry dependency** — Core defines the contract; ServiceDefaults handles infrastructure logging
- **No log persistence** — the implementation (database sink, Serilog enrichment, etc.) lives in the module that owns it
- Infrastructure logging (HTTP request/response logs, middleware logs) stays in ServiceDefaults

---

## 7. Audit Logging Contracts

DDD modules (Auth, Billing, API/Admin) need change tracking — who changed what, from what value, to what value. Core defines the contract. This is a stricter, more structured subset of logging: every entry captures old/new state for compliance and debugging.

### What goes in Core

```
Auditing/
  IAuditLogger.cs            # Interface for audit trail recording
  AuditEntry.cs              # Who, What, When, Module, EntityType, EntityId, Action, OldValues, NewValues
```

```csharp
public sealed record AuditEntry(
    Guid AuditId,
    string Module,
    string Action,
    string EntityType,
    string EntityId,
    Guid UserId,
    Dictionary<string, object?> OldValues,
    Dictionary<string, object?> NewValues,
    DateTimeOffset Timestamp);

public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
}
```

### What stays out
- **No EF Core interceptors** in Core — the mechanism that auto-captures changes belongs in the module's infrastructure
- **No audit query API** — reading audit trails belongs in the Admin module
- **No audit persistence** — the implementation (writing to an `audit.AuditLog` table) lives in its owning module

### Difference from Domain Logging

| Concern | Domain Logging | Audit Logging |
|---|---|---|
| Purpose | "Something happened" | "Who changed what from X to Y" |
| Captures | Action + context | Old value → New value |
| Used by | All modules | DDD modules (Auth, Billing, Admin) |
| Strictness | Informational | Compliance-grade (immutable trail) |

---

## Core vs ServiceDefaults Boundary

| Concern | Where | Why |
|---|---|---|
| OpenTelemetry / Metrics | ServiceDefaults | Aspire standard |
| Health Checks | ServiceDefaults | Infrastructure wiring |
| Serilog configuration | ServiceDefaults | Infrastructure |
| Result<T> / Error types | Core | Domain primitives |
| Base entities | Core | Dictates how modules store data |
| Integration event contracts | Core | Cross-module communication |
| ICurrentUserAccessor | Core | Cross-module identity mechanism |
| IDomainLogger / DomainLogEntry | Core | Cross-module structured logging contract |
| IAuditLogger / AuditEntry | Core | Cross-module audit trail contract |
| Global exception handler | API | HTTP boundary concern |
| Endpoint registration | API (per module) | Explicit `MapFulcrumXxxEndpoints()` |

---

## Final Folder Structure

```
Fulcrum.Core/
├── Fulcrum.Core.csproj          # Zero external NuGet dependencies
├── Auditing/
│   ├── IAuditLogger.cs
│   └── AuditEntry.cs
├── Domain/
│   ├── Entity.cs
│   ├── AggregateRoot.cs

│   └── IDomainEvent.cs
├── Errors/
│   ├── Error.cs
│   ├── ErrorType.cs
│   └── Result.cs
├── Events/
│   └── IIntegrationEvent.cs
├── Identity/
│   └── ICurrentUserAccessor.cs
└── Logging/
    ├── IDomainLogger.cs
    └── DomainLogEntry.cs
```

---

## Implementation Order

1. **Domain building blocks** — `Entity`, `AggregateRoot`, `IDomainEvent` (foundation for all modules)
2. **Error taxonomy** — `Error`, `ErrorType`, `Result<T>` (needed by every module's handlers)
3. **Logging contracts** — `IDomainLogger`, `DomainLogEntry` (all modules need structured logging from day one)
4. **Integration events** — `IIntegrationEvent` marker interface (Wolverine handles dispatch; Core defines contracts only)
5. **Audit contracts** — `IAuditLogger`, `AuditEntry` (DDD modules: Auth, Billing, Admin)
6. **Identity** — `ICurrentUserAccessor` (needed when Phase 2 implements Auth)

Steps 1-3 are prerequisites for Phase 2. Steps 4-6 align with Phase 2-3 timelines. Wolverine integration happens in `Fulcrum.API` when cross-module flows are first needed (Phase 3). Endpoint registration is explicit per module — no Core infrastructure needed.

---

## Verification

After implementation:
- `dotnet build` passes with zero warnings
- All module projects compile against Core without errors
- `dotnet format --verify-no-changes` passes
- No external package added to `Fulcrum.Core.csproj`
- `Directory.Packages.props` unchanged (Core has no packages to manage)
