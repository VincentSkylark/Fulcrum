# Phase 3 Implementation Plan: AI Orchestration (LLM Graph Engine)

**Created:** 2026-04-25
**Status:** Planning
**Branch:** `feature/phase3-ai-orchestration`
**Dependencies:** Phase 2 (Auth) complete

---

## Context

Phase 3 adds a lightweight graph-based state machine to `Fulcrum.Core` for orchestrating LLM calls and multi-step AI workflows. Think "lite LangGraph" — nodes do work, graphs own the wiring, state flows through immutably. This engine will power Phase 4 (News AI categorization, dedup) and Phase 5 (embeddings, recommendation scoring).

**Constraints:**
- Everything lives in `Fulcrum.Core` under `Fulcrum.Core.AI` namespace
- Single external NuGet dependency: `Microsoft.Extensions.AI.Abstractions` (abstractions-only, no transitive deps — same category as `Microsoft.Extensions.Logging.Abstractions`)
- LLM abstractions come from MEAI (`IChatClient`, `ChatMessage`, `ChatRole`) — no custom provider interfaces
- Provider implementations (Claude, OpenAI, Ollama) live in consuming modules via MEAI integration packages
- No checkpointing or persistence in v1

---

## Target File Structure

```
src/Fulcrum.Core/
├── AI/
│   ├── AgentState.cs                # LLM-04: Abstract state base
│   ├── GraphContext.cs              # LLM-04: Execution metadata wrapper
│   ├── INode.cs                     # LLM-01/02: Core node interface (single node type)
│   ├── TaskNode.cs                  # LLM-02: Convenience wrapper for Func
│   ├── IEdge.cs                     # LLM-01: Edge abstraction + concrete types
│   ├── IRouter.cs                   # LLM-05: Routing abstraction
│   ├── Graph.cs                     # LLM-01: Immutable graph definition
│   ├── GraphBuilder.cs              # LLM-03: Fluent builder API
│   ├── GraphExecutor.cs             # LLM-07: Execution engine
│   └── GraphExecutionException.cs   # Custom exception type
```

**Note:** LLM abstractions (`IChatClient`, `ChatMessage`, `ChatRole`, `IEmbeddingGenerator`) come from `Microsoft.Extensions.AI.Abstractions` (NuGet, v10.5.0). No custom `Providers/` folder needed.

---

## Step-by-Step Implementation

### Step 1: Core Abstractions (LLM-01, LLM-04)

**Files:** `AgentState.cs`, `GraphContext.cs`, `INode.cs`, `IEdge.cs`, `IGraph.cs`

**Note:** Chat primitives (`ChatMessage`, `ChatRole`) and LLM provider interfaces (`IChatClient`) come from `Microsoft.Extensions.AI.Abstractions` — no custom implementations needed.

**1a. State foundation**

```csharp
// Fulcrum.Core/AI/AgentState.cs
namespace Fulcrum.Core.AI;

/// <summary>
/// Base record for graph state. Nodes read this and return updates.
/// Consumers extend this with domain-specific fields.
/// </summary>
public abstract record AgentState;
```

```csharp
// Fulcrum.Core/AI/GraphContext.cs
using System.Collections.Immutable;

namespace Fulcrum.Core.AI;

/// <summary>
/// Engine metadata — separated from domain state (AgentState).
/// Carries turn counter, visit log, routing decision.
/// </summary>
public sealed record GraphContext<TState>(TState State) where TState : AgentState
{
    public int TotalIterations { get; init; }
    public ImmutableDictionary<string, int> NodeVisitCount { get; init; } =
        ImmutableDictionary<string, int>.Empty;
    public ImmutableList<string> NodeHistory { get; init; } = ImmutableList<string>.Empty;
    public string? PendingRouteTarget { get; init; }

    public GraphContext<TState> RecordVisit(string nodeId) => this with
    {
        TotalIterations = TotalIterations + 1,
        NodeHistory = NodeHistory.Add(nodeId),
        NodeVisitCount = NodeVisitCount.SetItem(
            nodeId, NodeVisitCount.GetValueOrDefault(nodeId, 0) + 1)
    };

    public GraphContext<TState> WithState(TState newState) => this with
    {
        State = newState,
        PendingRouteTarget = null
    };

    public GraphContext<TState> WithRoute(string targetNodeId) => this with
    {
        PendingRouteTarget = targetNodeId
    };
}
```

**1b. Node interface**

```csharp
// Fulcrum.Core/AI/INode.cs
namespace Fulcrum.Core.AI;

/// <summary>
/// Unit of work in the graph. Receives state, returns updated state.
/// </summary>
public interface INode<TState> where TState : AgentState
{
    string Id { get; }
    Task<TState> ExecuteAsync(TState state, CancellationToken ct);
}
```

**1c. Edge and graph definition**

```csharp
// Fulcrum.Core/AI/IEdge.cs
namespace Fulcrum.Core.AI;

/// <summary>
/// Represents a transition from one node to another.
/// </summary>
public interface IEdge<TState> where TState : AgentState
{
    string FromNodeId { get; }
    string ToNodeId { get; }
}

/// <summary>
/// Unconditional edge — always transitions to the target.
/// </summary>
public sealed record DirectEdge<TState>(
    string FromNodeId,
    string ToNodeId) : IEdge<TState> where TState : AgentState;

/// <summary>
/// Conditional edge — router determines the actual target at runtime.
/// </summary>
public sealed record ConditionalEdge<TState>(
    string FromNodeId,
    IRouter<TState> Router) : IEdge<TState> where TState : AgentState
{
    public string ToNodeId => string.Empty; // resolved at runtime
}
```

```csharp
// Fulcrum.Core/AI/IRouter.cs
namespace Fulcrum.Core.AI;

/// <summary>
/// Determines the next node based on current state.
/// Implemented by routing nodes or injected strategies.
/// </summary>
public interface IRouter<TState> where TState : AgentState
{
    Task<string> DetermineNextNodeAsync(TState state, CancellationToken ct);
}
```

```csharp
// Fulcrum.Core/AI/IGraph.cs
using System.Collections.Immutable;

namespace Fulcrum.Core.AI;

/// <summary>
/// Immutable graph definition. Created by GraphBuilder.
/// </summary>
public sealed class Graph<TState> where TState : AgentState
{
    public string EntryNodeId { get; }
    public ImmutableDictionary<string, INode<TState>> Nodes { get; }
    public ImmutableDictionary<string, IEdge<TState>> OutgoingEdges { get; }

    internal Graph(
        string entryNodeId,
        ImmutableDictionary<string, INode<TState>> nodes,
        ImmutableDictionary<string, IEdge<TState>> outgoingEdges)
    {
        EntryNodeId = entryNodeId;
        Nodes = nodes;
        OutgoingEdges = outgoingEdges;
    }
}
```

---

### Step 2: Graph Builder — Fluent API (LLM-03)

**File:** `GraphBuilder.cs`

```csharp
// Fulcrum.Core/AI/GraphBuilder.cs
using System.Collections.Immutable;

namespace Fulcrum.Core.AI;

public sealed class GraphBuilder<TState> where TState : AgentState
{
    private readonly Dictionary<string, INode<TState>> _nodes = [];
    private readonly Dictionary<string, IEdge<TState>> _edges = [];
    private string? _entryNodeId;

    /// <summary>
    /// Register a node in the graph.
    /// </summary>
    public GraphBuilder<TState> AddNode(INode<TState> node)
    {
        _nodes[node.Id] = node;
        return this;
    }

    /// <summary>
    /// Unconditional transition: after fromNode, always go to toNode.
    /// </summary>
    public GraphBuilder<TState> AddEdge(string fromNodeId, string toNodeId)
    {
        _edges[fromNodeId] = new DirectEdge<TState>(fromNodeId, toNodeId);
        return this;
    }

    /// <summary>
    /// Conditional transition: after fromNodeId, router decides the next node.
    /// </summary>
    public GraphBuilder<TState> AddConditionalEdge(string fromNodeId, IRouter<TState> router)
    {
        _edges[fromNodeId] = new ConditionalEdge<TState>(fromNodeId, router);
        return this;
    }

    /// <summary>
    /// Set the entry point of the graph.
    /// </summary>
    public GraphBuilder<TState> WithEntry(string nodeId)
    {
        _entryNodeId = nodeId;
        return this;
    }

    /// <summary>
    /// Build the immutable graph. Validates structure before returning.
    /// </summary>
    public Graph<TState> Build()
    {
        ArgumentNullException.ThrowIfNull(_entryNodeId);

        if (!_nodes.ContainsKey(_entryNodeId))
            throw new InvalidOperationException($"Entry node '{_entryNodeId}' not registered.");

        // Validate edge references point to existing nodes
        foreach (var edge in _edges.Values)
        {
            if (edge is DirectEdge<TState> direct && !_nodes.ContainsKey(direct.ToNodeId))
                throw new InvalidOperationException(
                    $"Edge from '{edge.FromNodeId}' references unknown node '{direct.ToNodeId}'.");
        }

        return new Graph<TState>(
            _entryNodeId,
            _nodes.ToImmutableDictionary(),
            _edges.ToImmutableDictionary());
    }
}
```

---

### Step 3: TaskNode — Convenience Wrapper (LLM-02)

**File:** `TaskNode.cs`

There is one node interface (`INode<TState>`) and one convenience wrapper (`TaskNode<TState>`). Nodes own all their logic — LLM calls, database queries, data transformation, or any combination. `IChatClient` (from MEAI) is injectable into any node that needs it.

```csharp
// Fulcrum.Core/AI/TaskNode.cs
namespace Fulcrum.Core.AI;

/// <summary>
/// Wraps a Func as an executable node. Use for simple one-off logic.
/// For anything more complex (DI, multi-step, LLM calls), implement INode directly.
/// </summary>
public sealed class TaskNode<TState>(
    string id,
    Func<TState, CancellationToken, Task<TState>> execute) : INode<TState>
    where TState : AgentState
{
    public string Id { get; } = id;

    public Task<TState> ExecuteAsync(TState state, CancellationToken ct) =>
        execute(state, ct);
}
```

**Usage example — a node that calls an LLM:**

```csharp
// In consuming module (e.g., Fulcrum.News)
// Nodes are just classes. Inject whatever you need.

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

**Why no dedicated `LlmNode`:** Prompt construction, response parsing, and post-LLM logic vary wildly per use case. A generic `LlmNode` would need so many configuration knobs it becomes a `TaskNode`. Nodes that call LLMs just inject `IChatClient` directly — clean, flexible, no extra abstraction.

---

### Step 4: Graph Execution Engine (LLM-07, LLM-08)

**File:** `GraphExecutor.cs`, `GraphExecutionException.cs`

```csharp
// Fulcrum.Core/AI/GraphExecutionException.cs
namespace Fulcrum.Core.AI;

public sealed class GraphExecutionException(string message) : Exception(message);
```

```csharp
// Fulcrum.Core/AI/GraphExecutor.cs
using System.Diagnostics;
using System.Collections.Immutable;

namespace Fulcrum.Core.AI;

/// <summary>
/// Runs a graph to completion. Two-tier cycle protection.
/// OpenTelemetry spans for observability.
/// </summary>
public sealed class GraphExecutor
{
    private const int MaxTotalIterations = 50;
    private const int MaxNodeVisits = 5;
    private const string EndNodeId = "END";

    private static readonly ActivitySource ActivitySource = new("Fulcrum.AI.Orchestrator");

    public async Task<TState> RunAsync<TState>(
        Graph<TState> graph,
        TState initialState,
        CancellationToken ct = default) where TState : AgentState
    {
        using var activity = ActivitySource.StartActivity("GraphExecution");
        activity?.SetTag("graph.entry_node", graph.EntryNodeId);

        var context = new GraphContext<TState>(initialState);
        var currentNodeId = graph.EntryNodeId;

        while (currentNodeId != EndNodeId)
        {
            ct.ThrowIfCancellationRequested();

            // 1. Resolve node
            if (!graph.Nodes.TryGetValue(currentNodeId, out var node))
                throw new GraphExecutionException(
                    $"Node '{currentNodeId}' not found in graph.");

            // 2. Execute node
            using var nodeSpan = ActivitySource.StartActivity($"Node:{currentNodeId}");
            nodeSpan?.SetTag("node.id", currentNodeId);

            var newState = await node.ExecuteAsync(context.State, ct);

            // 3. Update context
            context = context.RecordVisit(currentNodeId).WithState(newState);
            nodeSpan?.SetTag("context.total_iterations", context.TotalIterations);

            // 4. Cycle protection
            ValidateCycles(context, currentNodeId);

            // 5. Determine next node
            currentNodeId = ResolveNextNode(graph, context, currentNodeId);
        }

        activity?.SetTag("graph.total_iterations", context.TotalIterations);
        return context.State;
    }

    private static string ResolveNextNode<TState>(
        Graph<TState> graph,
        GraphContext<TState> context,
        string currentNodeId) where TState : AgentState
    {
        if (!graph.OutgoingEdges.TryGetValue(currentNodeId, out var edge))
            return EndNodeId; // No outgoing edge → terminal

        if (edge is DirectEdge<TState>)
            return edge.ToNodeId;

        if (edge is ConditionalEdge<TState> conditional)
        {
            // Router determines next target synchronously within the executor loop
            var target = conditional.Router.DetermineNextNodeAsync(
                context.State, CancellationToken.None).GetAwaiter().GetResult();
            // Note: Router call is sync here for simplicity. If routers need async I/O,
            // the executor loop should be adjusted to await here.
            // TODO: Make this properly async if routers need I/O.
            return target;
        }

        return EndNodeId;
    }

    private static void ValidateCycles<TState>(
        GraphContext<TState> context, string nodeId) where TState : AgentState
    {
        // Tier 1: Hard limit on total transitions
        if (context.TotalIterations > MaxTotalIterations)
            throw new GraphExecutionException(
                $"Maximum global iterations ({MaxTotalIterations}) exceeded.");

        // Tier 2: Soft limit on visits to the same node
        var visits = context.NodeVisitCount.GetValueOrDefault(nodeId, 0);
        if (visits > MaxNodeVisits)
            throw new GraphExecutionException(
                $"Node '{nodeId}' visited {visits} times — possible infinite loop.");
    }
}
```

**Note on `ResolveNextNode` async:** The router call needs to be async. The executor's main loop should `await` router resolution. The sync `.GetAwaiter().GetResult()` above is a placeholder — the final implementation will make the loop properly async throughout:

```csharp
// Correct async version — use this in the final code
private static async Task<string> ResolveNextNodeAsync<TState>(
    Graph<TState> graph,
    GraphContext<TState> context,
    string currentNodeId,
    CancellationToken ct) where TState : AgentState
{
    if (!graph.OutgoingEdges.TryGetValue(currentNodeId, out var edge))
        return EndNodeId;

    if (edge is DirectEdge<TState>)
        return edge.ToNodeId;

    if (edge is ConditionalEdge<TState> conditional)
        return await conditional.Router.DetermineNextNodeAsync(context.State, ct);

    return EndNodeId;
}
```

---

### Step 5: Tests

**File:** `tests/Fulcrum.Core.Tests/AI/`

```
tests/Fulcrum.Core.Tests/
└── AI/
    ├── GraphBuilderTests.cs          # Builder validation
    ├── GraphExecutorTests.cs         # Execution + cycle protection
    └── TaskNodeTests.cs              # TaskNode behavior
```

**Test cases:**

| Test | What It Validates |
|------|-------------------|
| `LinearGraph_ExecutesInOrder` | A → B → END: both nodes execute, state flows correctly |
| `ConditionalRouter_RoutesCorrectly` | Router returns "B" → B executes; returns "C" → C executes |
| `CycleProtection_GlobalLimit_Throws` | Graph exceeds 50 iterations → `GraphExecutionException` |
| `CycleProtection_NodeRevisit_Throws` | Same node visited >5 times → `GraphExecutionException` |
| `Cancellation_StopsMidExecution` | Token cancelled between nodes → `OperationCanceledException` |
| `MissingNode_Throws` | Edge references non-existent node → exception at build or runtime |
| `Builder_MissingEntryNode_Throws` | Build without entry node → `InvalidOperationException` |
| `Builder_InvalidEdge_Throws` | Edge points to unregistered node → `InvalidOperationException` |
| `TaskNode_ExecutesFunc_ReturnsState` | Func receives state, returns modified state |

**Test helper — test state:**

```csharp
internal sealed record TestState(string Value = "", int Step = 0) : AgentState;
```

---

## Implementation Order

| Order | Task | Requirement |
|-------|------|-------------|
| 1 | Core abstractions: `AgentState`, `GraphContext`, `INode` | LLM-01, LLM-04 |
| 2 | Edge types: `IEdge`, `DirectEdge`, `ConditionalEdge`, `IRouter` | LLM-01, LLM-05 |
| 3 | `Graph<TState>` immutable definition | LLM-01 |
| 4 | `GraphBuilder<TState>` fluent API | LLM-03 |
| 5 | `TaskNode<TState>` convenience wrapper | LLM-02 |
| 6 | `GraphExecutor` + `GraphExecutionException` | LLM-07 |
| 7 | Observability: ActivitySource spans on all nodes | LLM-08 |
| 8 | Tests: builder, executor, nodes, cycle protection, cancellation | All |

---

## Verification Checklist

- [ ] `dotnet build` — Fulcrum.Core compiles with only `Microsoft.Extensions.AI.Abstractions` as external dependency
- [ ] `dotnet test tests/Fulcrum.Core.Tests` — all AI tests pass
- [ ] `dotnet test` — no regressions in existing Auth/Core tests
- [ ] `dotnet format --verify-no-changes` — no formatting violations
- [ ] ActivitySource spans visible in Aspire Dashboard when running a test graph
- [ ] Custom `Providers/` folder deleted — `IChatClient` / `ChatMessage` / `ChatRole` from MEAI used instead

---

## Open Decisions

| Decision | Options | Default |
|----------|---------|---------|
| LLM abstractions | Custom `ILLMProvider` / `IStreamingLLMProvider` vs `Microsoft.Extensions.AI.Abstractions` | **MEAI adopted** — `IChatClient` replaces both custom interfaces; `ChatMessage` / `ChatRole` replace `PromptMessage` / `MessageRole` |
| Namespace | `Fulcrum.Core.AI` vs `Fulcrum.Core.Graph` | `Fulcrum.Core.AI` |
| END sentinel | Magic string `"END"` vs `INode` marker | Magic string (simplest for v1) |
| Router async in executor | Sync wrapper vs propagate async | Propagate async (correct approach) |
| Max iterations / node visits | Configurable per-graph vs constants | Constants in v1, configurable later |
