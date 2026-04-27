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
