namespace Fulcrum.Core.AI;

/// <summary>
/// Base record for graph state. Nodes read this and return updates.
/// Consumers extend this with domain-specific fields.
/// </summary>
public abstract record AgentState;
