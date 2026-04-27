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
