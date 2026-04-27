using Fulcrum.Core.AI;

namespace Fulcrum.Core.Tests.AI;

internal sealed record TestState(string Value = "", int Step = 0) : AgentState;
