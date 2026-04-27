using System.Diagnostics;

namespace Fulcrum.Core.AI;

/// <summary>
/// Runs a graph to completion with two-tier cycle protection and OpenTelemetry spans.
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

            if (!graph.Nodes.TryGetValue(currentNodeId, out var node))
                throw new GraphExecutionException($"Node '{currentNodeId}' not found in graph.");

            using var nodeSpan = ActivitySource.StartActivity($"Node:{currentNodeId}");
            nodeSpan?.SetTag("node.id", currentNodeId);

            var newState = await node.ExecuteAsync(context.State, ct);

            context = context.RecordVisit(currentNodeId).WithState(newState);
            nodeSpan?.SetTag("context.total_iterations", context.TotalIterations);

            ValidateCycles(context, currentNodeId);

            currentNodeId = await ResolveNextNodeAsync(graph, context, currentNodeId, ct);
        }

        activity?.SetTag("graph.total_iterations", context.TotalIterations);
        return context.State;
    }

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

    private static void ValidateCycles<TState>(
        GraphContext<TState> context, string nodeId) where TState : AgentState
    {
        if (context.TotalIterations > MaxTotalIterations)
            throw new GraphExecutionException(
                $"Maximum global iterations ({MaxTotalIterations}) exceeded.");

        var visits = context.NodeVisitCount.GetValueOrDefault(nodeId, 0);
        if (visits > MaxNodeVisits)
            throw new GraphExecutionException(
                $"Node '{nodeId}' visited {visits} times — possible infinite loop.");
    }
}
