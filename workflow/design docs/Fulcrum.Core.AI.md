# Fulcrum.Core.AI — Design Doc

## Context

Fulcrum.Core.AI is a lightweight graph-based state machine for orchestrating LLM calls and multi-step AI workflows. Think "lite LangGraph" — nodes do work, graphs own the wiring, state flows through immutably. This engine powers Phase 4 (News AI categorization, dedup) and Phase 5 (embeddings, recommendation scoring).

Core principles:
- **Minimal external dependencies** — Fulcrum.Core references only `Microsoft.Extensions.AI.Abstractions` (platform abstractions, no transitive deps)
- **Provider-agnostic** — `IChatClient` from MEAI is the LLM abstraction; concrete implementations (OpenAI, Anthropic, Ollama) live in consuming modules
- **Immutable graph definitions** — graphs are built once, executed many times
- **No persistence in v1** — no checkpointing, no state serialization

---

## 1. State Foundation

Two layers of state travel through execution: **domain state** (what the workflow cares about) and **engine metadata** (what the executor tracks).

### AgentState

Abstract base record. Consumers extend it with domain-specific fields (e.g., `NewsState` with `ArticleTitle`, `Category`).

```csharp
public abstract record AgentState;
```

### GraphContext\<TState\>

Wraps `AgentState` with execution metadata — visit counts, iteration counter, node history. Separated from domain state so nodes never see engine internals.

```csharp
public sealed record GraphContext<TState>(TState State) where TState : AgentState
{
    public int TotalIterations { get; init; }
    public ImmutableDictionary<string, int> NodeVisitCount { get; init; }
    public ImmutableList<string> NodeHistory { get; init; }
    public string? PendingRouteTarget { get; init; }
}
```

| Field | Purpose |
|---|---|
| `TotalIterations` | Global step counter — capped at 50 to catch runaway graphs |
| `NodeVisitCount` | Per-node visit count — capped at 5 to catch cycles |
| `NodeHistory` | Ordered list of visited node IDs (debugging/tracing) |
| `PendingRouteTarget` | Reserved for router-set targets (used by conditional edges) |

---

## 2. Nodes

### INode\<TState\>

Single node interface. Every node receives state and returns updated state.

```csharp
public interface INode<TState> where TState : AgentState
{
    string Id { get; }
    Task<TState> ExecuteAsync(TState state, CancellationToken ct);
}
```

### TaskNode\<TState\>

Convenience wrapper that turns a `Func<TState, CancellationToken, Task<TState>>` into a node. Use for simple one-off logic. For anything needing DI, multi-step orchestration, or LLM calls, implement `INode<TState>` directly as a class.

```csharp
public sealed class TaskNode<TState>(
    string id,
    Func<TState, CancellationToken, Task<TState>> execute) : INode<TState>
    where TState : AgentState;
```

### Why no LlmNode

Prompt construction, response parsing, and post-LLM logic vary wildly per use case. A generic `LlmNode` would need so many configuration knobs it becomes a `TaskNode`. Nodes that call LLMs inject `IChatClient` directly.

```csharp
// Consuming module — node injects its own dependencies
public sealed class CategorizeNode(IChatClient chatClient) : INode<NewsState>
{
    public string Id => "categorize";
    public async Task<NewsState> ExecuteAsync(NewsState state, CancellationToken ct)
    {
        var messages = new ChatMessage[]
        {
            new(ChatRole.System, "Categorize this article."),
            new(ChatRole.User, state.ArticleTitle)
        };
        var response = await chatClient.CompleteAsync(messages, ct);
        return state with { Category = response.Message.Text.Trim() };
    }
}
```

---

## 3. Edges and Routing

Edges define transitions between nodes. Each node has **at most one outgoing edge** — the last one registered wins.

### Edge Types

| Type | Behavior | When to Use |
|---|---|---|
| `DirectEdge` | Always transitions to a fixed target | Linear flow: A → B |
| `ConditionalEdge` | Router picks the target at runtime based on state | Branching: A → B if X, else C |

```csharp
// Direct: unconditional
builder.AddEdge("A", "B");

// Conditional: router decides
builder.AddConditionalEdge("A", new ApprovalRouter());
```

### IRouter\<TState\>

Determines the next node based on current state. Implemented by routing strategies injected at graph build time.

```csharp
public interface IRouter<TState> where TState : AgentState
{
    Task<string> DetermineNextNodeAsync(TState state, CancellationToken ct);
}
```

The router inspects `TState` and returns a node ID string. This is the single point of branching logic — the executor doesn't know or care about conditions.

### Branching Example

```csharp
public sealed class ApprovalRouter : IRouter<MyState>
{
    public Task<string> DetermineNextNodeAsync(MyState state, CancellationToken ct) =>
        Task.FromResult(state.IsApproved ? "ApprovedPath" : "RejectedPath");
}
```

---

## 4. Graph Definition and Builder

### Graph\<TState\>

Immutable graph. Created by `GraphBuilder`, never modified after construction.

```csharp
public sealed class Graph<TState> where TState : AgentState
{
    public string EntryNodeId { get; }
    public ImmutableDictionary<string, INode<TState>> Nodes { get; }
    public ImmutableDictionary<string, IEdge<TState>> OutgoingEdges { get; }
}
```

### GraphBuilder\<TState\>

Fluent API for constructing graphs. Validates at build time:
- Entry node must be registered
- Direct edges must reference existing nodes
- Conditional edges are validated at runtime (router can return any node ID)

```csharp
var graph = new GraphBuilder<MyState>()
    .AddNode(new TaskNode<MyState>("start", ...))
    .AddNode(new CategorizeNode(llm))
    .AddNode(new TaskNode<MyState>("store", ...))
    .AddEdge("start", "categorize")                    // unconditional
    .AddConditionalEdge("categorize", new RouteByCategory())  // conditional
    .AddEdge("store", "END")                           // END sentinel
    .WithEntry("start")
    .Build();
```

### END Sentinel

The magic string `"END"` marks a terminal node. When the executor resolves to `"END"`, the loop exits and returns the final state. Terminal nodes have no outgoing edge — the executor treats missing edges as `END`.

---

## 5. Execution Engine

### GraphExecutor

Runs a graph to completion with two-tier cycle protection and OpenTelemetry spans.

| Protection | Limit | What It Catches |
|---|---|---|
| Global iteration cap | 50 total transitions | Runaway graphs, missing terminal conditions |
| Per-node revisit cap | 5 visits to the same node | Infinite loops between nodes |

Execution loop:
1. Resolve current node from the graph
2. Execute the node (returns new state)
3. Record visit in context (iteration +1, visit count +1)
4. Validate cycle limits
5. Resolve next node via edge (direct → fixed ID, conditional → router, no edge → END)
6. Repeat until END

### OpenTelemetry

Every execution and every node gets an `ActivitySource` span (`Fulcrum.AI.Orchestrator`). Visible in the Aspire Dashboard for tracing graph runs.

---

## 6. LLM Provider Abstractions (Microsoft.Extensions.AI)

Fulcrum uses `Microsoft.Extensions.AI.Abstractions` (MEAI) for LLM provider abstractions instead of custom interfaces. MEAI is GA, Microsoft-backed, and provides the standard `IChatClient` interface that all major .NET AI SDKs implement (OpenAI, Anthropic, Ollama, Semantic Kernel).

```csharp
// IChatClient — replaces both ILLMProvider and IStreamingLLMProvider
// Supports both completion and streaming via the same interface
public interface IChatClient
{
    Task<ChatResponse> CompleteAsync(IList<ChatMessage> messages, ChatOptions? options, CancellationToken ct);
    IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(IList<ChatMessage> messages, ChatOptions? options, CancellationToken ct);
    ChatClientMetadata Metadata { get; }
}
```

### Chat Primitives (from MEAI)

```csharp
// ChatMessage — replaces PromptMessage
public sealed record ChatMessage(ChatRole Role, string Content);

// ChatRole — replaces MessageRole
public readonly struct ChatRole { ... } // System, User, Assistant, Tool, etc.
```

### Why MEAI over custom interfaces

- **One interface replaces two** — `IChatClient` handles both completion and streaming; no separate `ILLMProvider` / `IStreamingLLMProvider`
- **`IEmbeddingGenerator<TInput, TEmbedding>`** — available now for Phase 5 (embeddings, vector search) without designing a second provider abstraction
- **Middleware pipeline** — `ChatClientBuilder` with `.UseLogging()`, `.UseOpenTelemetry()`, `.UseDistributedCache()` — no manual wiring
- **Structured output** — `chatClient.CompleteAsync<T>(...)` maps responses to C# types directly
- **Tool/function calling** — built into `ChatOptions.Tools`, available when Phase 4/5 need it
- **Ecosystem compatibility** — OpenAI, Anthropic, Ollama, Semantic Kernel, MCP SDK all implement `IChatClient`

### Dependency Justification

`Microsoft.Extensions.AI.Abstractions` is an **abstractions-only** package with zero transitive dependencies — the same category as `Microsoft.Extensions.Logging.Abstractions`. This is the only NuGet dependency in Fulcrum.Core, and it's the kind of platform abstraction shared kernels are designed to reference.

Concrete provider implementations (`Microsoft.Extensions.AI.OpenAI`, etc.) live in consuming modules, registered via DI:

```csharp
// Consuming module's DI registration
builder.Services.AddChatClient(sp =>
    new ChatClientBuilder(new OpenAIClient(apiKey).GetChatClient("gpt-4.1").AsIChatClient())
        .UseLogging()
        .UseOpenTelemetry()
        .Build());
```

---

## 7. File Structure

```
src/Fulcrum.Core/
├── Fulcrum.Core.csproj             # References Microsoft.Extensions.AI.Abstractions
└── AI/
    ├── AgentState.cs                # Abstract state base
    ├── GraphContext.cs              # Execution metadata wrapper
    ├── INode.cs                     # Core node interface
    ├── TaskNode.cs                  # Func wrapper for simple nodes
    ├── IEdge.cs                     # Edge interface + DirectEdge + ConditionalEdge
    ├── IRouter.cs                   # Routing abstraction
    ├── Graph.cs                     # Immutable graph definition
    ├── GraphBuilder.cs              # Fluent builder API
    ├── GraphExecutor.cs             # Execution engine with cycle protection
    └── GraphExecutionException.cs   # Custom exception type
```

Flat structure within `AI/` — 10 files, all tightly coupled. LLM abstractions come from `Microsoft.Extensions.AI.Abstractions` (no custom `Providers/` folder needed). Subdirectories only if the module grows past ~20 files or gains a second major concept beyond graph execution.

---

## 8. What Stays Out (v1)

| Concern | Reason |
|---|---|
| Checkpointing / state persistence | No storage dependencies in Core. v2 concern. |
| LlmNode abstraction | Too many knobs — nodes inject `IChatClient` directly |
| Tool/function calling abstractions | Available via MEAI `ChatOptions.Tools` when Phase 4/5 define the use case |
| Multi-agent orchestration | Single-graph execution is sufficient for v1 |
| Graph serialization / visualization | Build-time tooling, not a runtime concern |

---

## 9. Test Coverage

Tests live in `tests/Fulcrum.Core.Tests/AI/`.

| Test | Validates |
|---|---|
| `LinearGraph_ExecutesInOrder` | A → B → END: both nodes execute, state flows |
| `ConditionalRouter_RoutesCorrectly` | Router returns "B" → B executes; "C" → C executes |
| `CycleProtection_GlobalLimit_Throws` | >50 iterations → `GraphExecutionException` |
| `CycleProtection_NodeRevisit_Throws` | Same node >5 visits → `GraphExecutionException` |
| `Cancellation_StopsMidExecution` | Token cancelled between nodes → `OperationCanceledException` |
| `MissingNode_Throws` | Edge references non-existent node → exception |
| `Builder_MissingEntryNode_Throws` | Build without entry → `InvalidOperationException` |
| `Builder_InvalidEdge_Throws` | Direct edge to unregistered node → `InvalidOperationException` |
| `TaskNode_ExecutesFunc_ReturnsState` | Func receives state, returns modified state |

---

## 10. Open Decisions

| Decision | Current Choice | Future Consideration |
|---|---|---|
| LLM abstractions | `Microsoft.Extensions.AI.Abstractions` (GA, v10.5.0) | Provides `IChatClient`, `IEmbeddingGenerator`, `ChatMessage`, `ChatRole` — industry standard |
| END sentinel | Magic string `"END"` | Could become a marker interface or static constant |
| Cycle limits | Constants (50 global, 5 per-node) | Configurable per-graph in v2 |
| Router async | Fully async | No change needed — already correct |
| Edge cardinality | One outgoing edge per node | Could support fan-out / parallel branches later |
| Namespace | `Fulcrum.Core.AI` | Stays unless the module becomes its own project |
