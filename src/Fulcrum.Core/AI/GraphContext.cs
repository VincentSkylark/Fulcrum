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
