using FluentAssertions;
using Fulcrum.Core.AI;
using Xunit;

namespace Fulcrum.Core.Tests.AI;

public sealed class GraphBuilderTests
{
    [Fact]
    public void BuildWithValidGraphReturnsGraph()
    {
        // Arrange
        var nodeA = new TaskNode<TestState>("a", (s, _) => Task.FromResult(s with { Step = s.Step + 1 }));
        var nodeB = new TaskNode<TestState>("b", (s, _) => Task.FromResult(s with { Step = s.Step + 1 }));

        // Act
        var graph = new GraphBuilder<TestState>()
            .AddNode(nodeA)
            .AddNode(nodeB)
            .AddEdge("a", "b")
            .WithEntry("a")
            .Build();

        // Assert
        graph.EntryNodeId.Should().Be("a");
        graph.Nodes.Should().ContainKeys("a", "b");
    }

    [Fact]
    public void BuildMissingEntryNodeThrows()
    {
        // Arrange
        var nodeA = new TaskNode<TestState>("a", (s, _) => Task.FromResult(s));
        var builder = new GraphBuilder<TestState>()
            .AddNode(nodeA);

        // Act
        var act = () => builder.Build();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildEntryNodeNotRegisteredThrows()
    {
        // Arrange
        var nodeA = new TaskNode<TestState>("a", (s, _) => Task.FromResult(s));
        var builder = new GraphBuilder<TestState>()
            .AddNode(nodeA)
            .WithEntry("missing");

        // Act
        var act = () => builder.Build();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'missing'*not registered*");
    }

    [Fact]
    public void BuildEdgeToUnknownNodeThrows()
    {
        // Arrange
        var nodeA = new TaskNode<TestState>("a", (s, _) => Task.FromResult(s));
        var builder = new GraphBuilder<TestState>()
            .AddNode(nodeA)
            .AddEdge("a", "nonexistent")
            .WithEntry("a");

        // Act
        var act = () => builder.Build();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*unknown node*");
    }

    [Fact]
    public void BuildWithConditionalEdgeSucceeds()
    {
        // Arrange
        var nodeA = new TaskNode<TestState>("a", (s, _) => Task.FromResult(s));
        var nodeB = new TaskNode<TestState>("b", (s, _) => Task.FromResult(s));
        var nodeC = new TaskNode<TestState>("c", (s, _) => Task.FromResult(s));
        var router = new TestRouter(_ => "b");

        // Act
        var graph = new GraphBuilder<TestState>()
            .AddNode(nodeA)
            .AddNode(nodeB)
            .AddNode(nodeC)
            .AddConditionalEdge("a", router)
            .WithEntry("a")
            .Build();

        // Assert
        graph.OutgoingEdges.Should().ContainKey("a");
        graph.OutgoingEdges["a"].Should().BeOfType<ConditionalEdge<TestState>>();
    }
}

file sealed class TestRouter(Func<TestState, string> route) : IRouter<TestState>
{
    public Task<string> DetermineNextNodeAsync(TestState state, CancellationToken ct) =>
        Task.FromResult(route(state));
}
