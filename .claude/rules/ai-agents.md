# AI Agent Rules

## Module Structure

`Fulcrum.Core.AI` is a graph-based state machine for orchestrating LLM workflows. It lives entirely in `src/Fulcrum.Core/AI/` under the `Fulcrum.Core.AI` namespace. LLM abstractions come from `Microsoft.Extensions.AI.Abstractions` — there are no custom provider interfaces in Core.

### API Surface

| Type | Purpose | File |
|---|---|---|
| `AgentState` | Abstract base record — extend with domain fields | `AgentState.cs` |
| `GraphContext<TState>` | Engine metadata (iterations, visit counts, history) | `GraphContext.cs` |
| `INode<TState>` | Unit of work — receives state, returns updated state | `INode.cs` |
| `TaskNode<TState>` | Func wrapper for simple one-off nodes | `TaskNode.cs` |
| `IEdge<TState>` | Transition between nodes | `IEdge.cs` |
| `DirectEdge<TState>` | Unconditional transition (A → B) | `IEdge.cs` |
| `ConditionalEdge<TState>` | Runtime router-based transition (A → B if X, else C) | `IEdge.cs` |
| `IRouter<TState>` | Determines next node based on state | `IRouter.cs` |
| `Graph<TState>` | Immutable graph definition (created by builder) | `Graph.cs` |
| `GraphBuilder<TState>` | Fluent builder API | `GraphBuilder.cs` |
| `GraphExecutor` | Execution engine with cycle protection + OpenTelemetry | `GraphExecutor.cs` |
| `GraphExecutionException` | Thrown on cycle violations | `GraphExecutionException.cs` |

### LLM Abstractions (from MEAI)

| Type | Replaces |
|---|---|
| `IChatClient` | Custom `ILLMProvider` / `IStreamingLLMProvider` |
| `ChatMessage` | Custom `PromptMessage` |
| `ChatRole` | Custom `MessageRole` |
| `IEmbeddingGenerator<TInput,TEmbedding>` | Phase 5 — no custom interface needed |

---

## Building an Agent

### Step 1: Define State

Extend `AgentState` with a sealed record containing domain fields. State is immutable — nodes return `state with { ... }`.

```csharp
public sealed record CategorizeState(
    string ArticleTitle,
    string? Category = null,
    double Confidence = 0.0) : AgentState;
```

### Step 2: Implement Nodes

Each node implements `INode<TState>` as a sealed class with a primary constructor for DI. Nodes own their logic entirely — LLM calls, database queries, transformations, or any combination.

```csharp
public sealed class CategorizeNode(IChatClient chatClient) : INode<CategorizeState>
{
    public string Id => "categorize";

    public async Task<CategorizeState> ExecuteAsync(CategorizeState state, CancellationToken ct)
    {
        var messages = new ChatMessage[]
        {
            new(ChatRole.System, "Categorize this article into one of: Tech, Science, Politics, Sports, Entertainment."),
            new(ChatRole.User, state.ArticleTitle)
        };
        var response = await chatClient.CompleteAsync(messages, ct);
        return state with { Category = response.Message.Text.Trim() };
    }
}
```

- **Use `TaskNode<TState>`** for simple one-off logic that doesn't need DI:
  ```csharp
  new TaskNode<MyState>("transform", (state, ct) => Task.FromResult(state with { Value = state.Value.ToUpper() }))
  ```
- **Implement `INode<TState>` directly** when the node needs DI (IChatClient, DbContext, etc.).

### Step 3: Implement Routers (for branching)

Routers inspect state and return a target node ID string. Use `IRouter<TState>` for any branching logic.

```csharp
public sealed class CategoryRouter : IRouter<CategorizeState>
{
    public Task<string> DetermineNextNodeAsync(CategorizeState state, CancellationToken ct) =>
        Task.FromResult(state.Confidence > 0.8 ? "auto-approve" : "human-review");
}
```

### Step 4: Build the Graph

Use `GraphBuilder<TState>` with the fluent API. Validate structure at build time.

```csharp
var graph = new GraphBuilder<CategorizeState>()
    .AddNode(new CategorizeNode(chatClient))
    .AddNode(new TaskNode<CategorizeState>("validate", ValidateCategory))
    .AddNode(new TaskNode<CategorizeState>("store", StoreCategory))
    .AddEdge("categorize", "validate")                    // unconditional
    .AddConditionalEdge("validate", new CategoryRouter()) // conditional
    .AddEdge("auto-approve", "store")                     // unconditional
    .AddEdge("human-review", "END")                       // terminal
    .AddEdge("store", "END")                              // terminal
    .WithEntry("categorize")
    .Build();
```

### Step 5: Execute

Inject `GraphExecutor` and run the graph. Execution is fully async with `CancellationToken` support.

```csharp
public sealed class CategorizeAgent(GraphExecutor executor)
{
    public async Task<CategorizeState> RunAsync(string articleTitle, CancellationToken ct)
    {
        var graph = BuildGraph(); // build once, cache if reused
        return await executor.RunAsync(graph, new CategorizeState(articleTitle), ct);
    }
}
```

---

## Conventions

### Where Agents Live

- **State records** and **node/router classes** live in the consuming module (e.g., `src/Fulcrum.News/Agents/Categorize/`).
- **Graph building** and **execution** happen in the consuming module's service layer.
- **`GraphExecutor`** is registered as a singleton in DI — inject it where needed.
- **`IChatClient`** is registered in the consuming module's DI via MEAI middleware pipeline:
  ```csharp
  builder.Services.AddChatClient(sp => ...)
      .UseLogging()
      .UseOpenTelemetry()
      .Build();
  ```

### File Organization per Agent

```
src/Fulcrum.News/
└── Agents/
    └── Categorize/
        ├── CategorizeState.cs        # State record
        ├── CategorizeNode.cs         # LLM call node
        ├── ValidateNode.cs           # Business logic node
        ├── CategoryRouter.cs         # Branching logic
        └── CategorizeAgent.cs        # Builds graph, exposes RunAsync
```

### Node Design

- **One responsibility per node.** A node does one thing: call an LLM, query a database, transform data, or make a routing decision.
- **Inject dependencies via primary constructor.** `IChatClient`, `DbContext`, `ILogger` — whatever the node needs.
- **Return immutable state.** Always `return state with { ... }`. Never mutate state in place.
- **Propagate `CancellationToken`.** Pass `ct` to all async calls inside the node.
- **Don't catch exceptions in nodes.** Let them propagate to `GraphExecutor`. The executor handles the lifecycle.

### Router Design

- **Routers are stateless.** They inspect `TState` and return a node ID string. No side effects.
- **Return registered node IDs only.** Returning an unregistered ID causes a `GraphExecutionException` at runtime.
- **Keep routing logic simple.** If a router needs to call a database or LLM, extract that into a preceding node and route on the result.

### Graph Building

- **Build once, execute many times.** Construct the graph in the agent's constructor or a factory method. `Graph<TState>` is immutable and thread-safe.
- **Always set an entry node** via `WithEntry()`. Missing entry throws `InvalidOperationException` at build time.
- **Direct edges are validated at build time.** If a `DirectEdge` points to an unregistered node, `Build()` throws.
- **Terminal nodes have no outgoing edge.** The executor treats missing edges as `END`.
- **One outgoing edge per node.** The last edge registered for a node ID wins.

### Execution

- **Cycle protection is built-in:** max 50 total iterations, max 5 visits per node. Exceeding either throws `GraphExecutionException`.
- **OpenTelemetry spans** are emitted automatically (`Fulcrum.AI.Orchestrator` activity source). Visible in the Aspire Dashboard.
- **Cancellation is checked between nodes.** A cancelled token throws `OperationCanceledException` — no partial state corruption.

---

## Anti-Patterns

- **Don't create a generic `LlmNode`.** Prompt construction, response parsing, and post-LLM logic vary per use case. Implement `INode<TState>` directly and inject `IChatClient`.
- **Don't put agent state in `AgentState` base.** Domain fields go in the consuming module's sealed record. `AgentState` is always empty — it's a type-level marker.
- **Don't reference `Providers/` or custom LLM interfaces.** They no longer exist. Use `IChatClient`, `ChatMessage`, `ChatRole` from `Microsoft.Extensions.AI.Abstractions`.
- **Don't build graphs per request.** Build once, cache the `Graph<TState>` instance. Only the initial state (`TState`) changes per execution.
- **Don't skip `CancellationToken`.** Every `ExecuteAsync` and `DetermineNextNodeAsync` receives one — pass it through to all async calls.
- **Don't mutate state in place.** Always return `state with { ... }`. The engine relies on immutable state transitions for correctness and debugging.

---

## Quick Reference

| Task | Approach |
|---|---|
| Define workflow state | `sealed record MyState(...) : AgentState` |
| Simple node (no DI) | `new TaskNode<MyState>("id", (s, ct) => ...)` |
| Node with LLM | `sealed class MyNode(IChatClient chatClient) : INode<MyState>` |
| Node with DB | `sealed class MyNode(AppDbContext db) : INode<MyState>` |
| Branching logic | `sealed class MyRouter : IRouter<MyState>` |
| Linear flow | `builder.AddEdge("a", "b")` |
| Conditional flow | `builder.AddConditionalEdge("a", new MyRouter())` |
| Terminal node | No outgoing edge, or `builder.AddEdge("x", "END")` |
| Execute graph | `await executor.RunAsync(graph, initialState, ct)` |
| Register LLM | `builder.Services.AddChatClient(...).UseLogging().Build()` |
