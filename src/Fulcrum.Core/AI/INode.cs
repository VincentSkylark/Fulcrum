namespace Fulcrum.Core.AI;

/// <summary>
/// Unit of work in the graph. Receives state, returns updated state.
/// </summary>
public interface INode<TState> where TState : AgentState
{
    string Id { get; }
    Task<TState> ExecuteAsync(TState state, CancellationToken ct);
}
