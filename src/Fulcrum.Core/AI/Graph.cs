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
