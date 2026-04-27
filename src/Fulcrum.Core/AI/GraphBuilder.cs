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
