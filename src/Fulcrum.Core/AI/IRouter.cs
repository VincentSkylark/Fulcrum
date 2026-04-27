namespace Fulcrum.Core.AI;

/// <summary>
/// Determines the next node based on current state.
/// Implemented by routing nodes or injected strategies.
/// </summary>
public interface IRouter<TState> where TState : AgentState
{
    Task<string> DetermineNextNodeAsync(TState state, CancellationToken ct);
}
