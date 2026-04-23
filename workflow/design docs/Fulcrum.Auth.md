# Fulcrum.Auth — Design Document

## Overview

Fulcrum.Auth is the authentication and identity module. It wraps Ory Kratos (self-hosted) and exposes session management and auth proxy endpoints to the rest of the application. Other modules never interact with Kratos directly — they use `ICurrentUserAccessor` from Fulcrum.Core.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Identity provider | Ory Kratos (self-hosted) | Avoids rebuilding auth; supports sessions, social login, MFA; open source |
| Kratos database | Separate DB (`kratos-db`) on shared PostgreSQL | Same server, isolated data, one Postgres to manage |
| Kratos SDK | `Ory.Client` NuGet package | Official .NET SDK; typed models for all Kratos APIs |
| Session validation | Middleware calling Kratos `toSession` API | Cookie-based for web; token-based for API; middleware populates `ICurrentUserAccessor` |
| Endpoint pattern | Proxy to Kratos public API flows | API owns the HTTP surface; Kratos handles the identity logic |
| Profile entity | Local EF Core entity in `auth` schema | Tracks app-specific user data (tier, preferences) not stored in Kratos |
| Flow pattern | Server-driven single POST | Backend creates + submits Kratos flows in one request; simpler for API clients |

## Project Structure

```
src/Fulcrum.Auth/
├── Fulcrum.Auth.csproj
├── ServiceCollectionExtensions.cs      # AddFulcrumAuth() + MapFulcrumAuthEndpoints()
├── Configuration/
│   └── KratosOptions.cs                # Options pattern for Kratos base URLs
├── Clients/
│   └── IKratosClient.cs                # Thin wrapper over Ory SDK
├── Middleware/
│   └── SessionMiddleware.cs            # Validates session, populates ICurrentUserAccessor
├── Identity/
│   └── CurrentUserAccessor.cs          # ICurrentUserAccessor implementation (AsyncLocal)
├── Endpoints/
│   ├── LoginEndpoints.cs               # POST /api/auth/login
│   ├── RegisterEndpoints.cs            # POST /api/auth/register
│   ├── RecoveryEndpoints.cs            # POST /api/auth/recovery
│   ├── VerificationEndpoints.cs        # POST /api/auth/verify
│   └── SessionEndpoints.cs             # GET /api/auth/session
└── Data/
    ├── AuthDbContext.cs                # EF Core DbContext for auth schema
    ├── UserProfile.cs                  # App-specific user data (tier, display name)
    └── Migrations/                     # EF Core migrations
```

## Module Boundaries

```
Fulcrum.API (Host)
  ├── calls AddFulcrumAuth() for DI registration
  ├── calls MapFulcrumAuthEndpoints() for endpoints
  ├── adds app.UseMiddleware<SessionMiddleware>() to pipeline
  └── reads KratosOptions from appsettings

Fulcrum.Auth
  ├── references Fulcrum.Core (ICurrentUserAccessor, Result<T>, Entity)
  ├── references Ory.Client NuGet package
  ├── owns all Kratos communication
  └── owns the auth PostgreSQL schema

Fulcrum.Core
  └── defines ICurrentUserAccessor (already exists)

Other modules (News, Billing, etc.)
  └── inject ICurrentUserAccessor — never reference Kratos directly
```

## Kratos Infrastructure

### Local Development (Aspire)

Kratos runs as a container managed by the Aspire AppHost:
- Image: `oryd/kratos:v1.3.1`
- Public API: port 4433 (browser-facing flows)
- Admin API: port 4434 (server-side identity management)
- Database: `kratos-db` on shared PostgreSQL instance
- Config: bind-mounted from `kratos/config/` directory
- Auto-migration: `--dev` flag runs schema migrations on startup

### Config Files Required

```
kratos/
├── config/
│   ├── kratos.yml                  # Serve config, self-service flows, DSN
│   └── identity.schema.json        # User traits: email, name
└── .gitkeep
```

### Production

Self-hosted Kratos via Docker alongside the API:
- Same config files with production values
- Separate PostgreSQL database (same server or dedicated)
- Secrets via environment variables (cookie secret, cipher secret)
- No `--dev` flag in production

## Session Flow

```
Browser → API Request (with cookie)
  → SessionMiddleware
    → IKratosClient.ToSessionAsync(cookie/token)
      → Kratos Admin API GET /sessions/whoami
        → Returns Session (identity_id, traits, expires_at)
    → Populate CurrentUserAccessor (UserId, Email, Tier, IsAuthenticated)
  → Endpoint handler (can inject ICurrentUserAccessor)
```

- Cookie-based for browser clients (Kratos sets `ory_kratos_session` cookie)
- Token-based for API/mobile clients (`X-Session-Token` header)
- Middleware is opt-in per endpoint via `RequireAuthorization()` or endpoint-specific checks
- Unauthenticated requests pass through with `IsAuthenticated = false`
- Tier comes from the `UserProfile` entity in `auth.user_profiles`, not from Kratos traits

## Endpoint Design

All endpoints proxy to Kratos public API flows (server-driven single POST pattern).

| Endpoint | Method | Kratos Flow | Purpose |
|----------|--------|-------------|---------|
| `/api/auth/login` | POST | Login flow (create + submit) | Email/password login |
| `/api/auth/register` | POST | Registration flow (create + submit) | New account creation |
| `/api/auth/recovery` | POST | Recovery flow (create + submit) | Password reset |
| `/api/auth/verify` | POST | Verification flow (get + submit) | Email verification |
| `/api/auth/session` | GET | toSession | Current user info |

### Flow Pattern (Login example)

```
1. Client POSTs /api/auth/login with { email, password }
2. Endpoint creates a Kratos login flow: POST /self-service/login/api
3. Endpoint submits the flow with credentials: POST /self-service/login?flow={id}
4. On success: return session token + set cookie
5. On failure: return Kratos error messages as ProblemDetails
```

## Key Types

### KratosOptions (Configuration)
```csharp
namespace Fulcrum.Auth.Configuration;

public sealed record KratosOptions
{
    public string PublicBaseUrl { get; init; } = "http://localhost:4433/";
    public string AdminBaseUrl { get; init; } = "http://localhost:4434/";
}
```

### IKratosClient (Clients)
```csharp
namespace Fulcrum.Auth.Clients;

public interface IKratosClient
{
    Task<Result<SessionInfo>> ValidateSessionAsync(string cookieOrToken, CancellationToken ct);
    Task<Result<SessionInfo>> SubmitLoginFlowAsync(string email, string password, CancellationToken ct);
    Task<Result<SessionInfo>> SubmitRegistrationFlowAsync(string email, string password, string firstName, string lastName, CancellationToken ct);
    Task<Result> SubmitRecoveryFlowAsync(string email, CancellationToken ct);
    Task<Result> VerifyEmailAsync(string flowId, string code, CancellationToken ct);
}
```

### SessionInfo (DTO)
```csharp
namespace Fulcrum.Auth.Clients;

public sealed record SessionInfo(
    Guid IdentityId,
    string Email,
    string? FirstName,
    string? LastName,
    DateTimeOffset ExpiresAt);
```

### CurrentUserAccessor (Identity)
```csharp
namespace Fulcrum.Auth.Identity;

internal sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private static readonly AsyncLocal<CurrentUser?> _current = new();

    public Guid UserId => _current.Value?.UserId ?? Guid.Empty;
    public string Email => _current.Value?.Email ?? string.Empty;
    public string Tier => _current.Value?.Tier ?? "free";
    public bool IsAuthenticated => _current.Value is not null;

    internal void Set(CurrentUser user) => _current.Value = user;
    internal void Clear() => _current.Value = null;
}

internal sealed record CurrentUser(Guid UserId, string Email, string Tier);
```

### UserProfile (Data)
```csharp
namespace Fulcrum.Auth.Data;

public sealed class UserProfile : Entity
{
    public Guid KratosIdentityId { get; init; }
    public string Email { get; init; }
    public string FirstName { get; set; }
    public string? LastName { get; set; }
    public string Tier { get; set; } = "free";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### AuthDbContext (Data)
```csharp
namespace Fulcrum.Auth.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>(e =>
        {
            e.ToTable("user_profiles", "auth");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.KratosIdentityId).IsUnique();
            e.HasIndex(p => p.Email).IsUnique();
        });
    }
}
```
