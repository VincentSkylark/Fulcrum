using FluentAssertions;
using Fulcrum.Core.AI;
using Xunit;

namespace Fulcrum.Core.Tests.AI;

public sealed class GraphExecutorTests
{
    private readonly GraphExecutor _executor = new();

    [Fact]
    public async Task LinearGraphExecutesInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var nodeA = new TaskNode<TestState>("a", (s, _) =>
        {
            executionOrder.Add("a");
            return Task.FromResult(s with { Value = "ran-a", Step = 1 });
        });
        var nodeB = new TaskNode<TestState>("b", (s, _) =>
        {
            executionOrder.Add("b");
            return Task.FromResult(s with { Value = s.Value + "|ran-b", Step = 2 });
        });

        var graph = new GraphBuilder<TestState>()
            .AddNode(nodeA)
            .AddNode(nodeB)
            .AddEdge("a", "b")
            .WithEntry("a")
            .Build();

        // Act
        var result = await _executor.RunAsync(graph, new TestState(), TestContext.Current.CancellationToken);

        // Assert
        executionOrder.Should().Equal("a", "b");
        result.Value.Should().Be("ran-a|ran-b");
        result.Step.Should().Be(2);
    }

    [Fact]
    public async Task SingleNodeWithNoOutgoingEdgeTerminates()
    {
        // Arrange
        var node = new TaskNode<TestState>("only", (s, _) =>
            Task.FromResult(s with { Value = "done" }));

        var graph = new GraphBuilder<TestState>()
            .AddNode(node)
            .WithEntry("only")
            .Build();

        // Act
        var result = await _executor.RunAsync(graph, new TestState(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be("done");
    }

    [Fact]
    public async Task ConditionalRouterRoutesCorrectly()
    {
        // Arrange
        var executionOrder = new List<string>();
        var nodeA = new TaskNode<TestState>("a", (s, _) =>
        {
            executionOrder.Add("a");
            return Task.FromResult(s with { Value = "to-b" });
        });
        var nodeB = new TaskNode<TestState>("b", (s, _) =>
        {
            executionOrder.Add("b");
            return Task.FromResult(s with { Value = "reached-b" });
        });
        var nodeC = new TaskNode<TestState>("c", (s, _) =>
        {
            executionOrder.Add("c");
            return Task.FromResult(s with { Value = "reached-c" });
        });
        var router = new FuncRouter<TestState>(s =>
            s.Value == "to-b" ? "b" : "c");

        var graph = new GraphBuilder<TestState>()
            .AddNode(nodeA)
            .AddNode(nodeB)
            .AddNode(nodeC)
            .AddConditionalEdge("a", router)
            .WithEntry("a")
            .Build();

        // Act
        var result = await _executor.RunAsync(graph, new TestState(), TestContext.Current.CancellationToken);

        // Assert
        executionOrder.Should().Equal("a", "b");
        result.Value.Should().Be("reached-b");
    }

    [Fact]
    public async Task ConditionalRouterRoutesToAlternate()
    {
        // Arrange
        var executionOrder = new List<string>();
        var nodeA = new TaskNode<TestState>("a", (s, _) =>
        {
            executionOrder.Add("a");
            return Task.FromResult(s with { Value = "to-c" });
        });
        var nodeB = new TaskNode<TestState>("b", (s, _) =>
        {
            executionOrder.Add("b");
            return Task.FromResult(s);
        });
        var nodeC = new TaskNode<TestState>("c", (s, _) =>
        {
            executionOrder.Add("c");
            return Task.FromResult(s with { Value = "reached-c" });
        });
        var router = new FuncRouter<TestState>(s =>
            s.Value == "to-b" ? "b" : "c");

        var graph = new GraphBuilder<TestState>()
            .AddNode(nodeA)
            .AddNode(nodeB)
            .AddNode(nodeC)
            .AddConditionalEdge("a", router)
            .WithEntry("a")
            .Build();

        // Act
        var result = await _executor.RunAsync(graph, new TestState(), TestContext.Current.CancellationToken);

        // Assert
        executionOrder.Should().Equal("a", "c");
        result.Value.Should().Be("reached-c");
    }

    [Fact]
    public async Task CycleProtectionGlobalLimitThrows()
    {
        // Arrange — create a cycle: a → b → a → b → ...
        // The two-tier protection will throw GraphExecutionException
        var nodeA = new TaskNode<TestState>("a", (s, _) => Task.FromResult(s));
        var nodeB = new TaskNode<TestState>("b", (s, _) => Task.FromResult(s));

        var graph = new GraphBuilder<TestState>()
            .AddNode(nodeA)
            .AddNode(nodeB)
            .AddEdge("a", "b")
            .AddEdge("b", "a")
            .WithEntry("a")
            .Build();

        // Act
        var act = () => _executor.RunAsync(graph, new TestState(), TestContext.Current.CancellationToken);

        // Assert — cycle protection catches it (either global or per-node limit)
        await act.Should().ThrowAsync<GraphExecutionException>();
    }

    [Fact]
    public async Task CycleProtectionNodeRevisitThrows()
    {
        // Arrange — node "loop" visits itself via a small cycle that revisits it often
        var callCount = 0;
        var router = new FuncRouter<TestState>(_ =>
        {
            callCount++;
            return callCount < 10 ? "loop" : "done";
        });

        var loopNode = new TaskNode<TestState>("loop", (s, _) => Task.FromResult(s));
        var doneNode = new TaskNode<TestState>("done", (s, _) => Task.FromResult(s));

        var graph = new GraphBuilder<TestState>()
            .AddNode(loopNode)
            .AddNode(doneNode)
            .AddConditionalEdge("loop", router)
            .WithEntry("loop")
            .Build();

        // Act
        var act = () => _executor.RunAsync(graph, new TestState(), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<GraphExecutionException>()
            .WithMessage("*visited*times*infinite loop*");
    }

    [Fact]
    public async Task CancellationStopsMidExecution()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var nodeA = new TaskNode<TestState>("a", async (s, ct) =>
        {
            await Task.Delay(100, ct);
            return s with { Value = "a-done" };
        });

        var graph = new GraphBuilder<TestState>()
            .AddNode(nodeA)
            .WithEntry("a")
            .Build();

        await cts.CancelAsync();

        // Act
        var act = () => _executor.RunAsync(graph, new TestState(), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task MultiStepGraphStateFlowsCorrectly()
    {
        // Arrange — 4-step pipeline
        var step1 = new TaskNode<TestState>("step1", (s, _) =>
            Task.FromResult(s with { Value = "step1", Step = 1 }));
        var step2 = new TaskNode<TestState>("step2", (s, _) =>
            Task.FromResult(s with { Value = s.Value + "|step2", Step = 2 }));
        var step3 = new TaskNode<TestState>("step3", (s, _) =>
            Task.FromResult(s with { Value = s.Value + "|step3", Step = 3 }));
        var step4 = new TaskNode<TestState>("step4", (s, _) =>
            Task.FromResult(s with { Value = s.Value + "|step4", Step = 4 }));

        var graph = new GraphBuilder<TestState>()
            .AddNode(step1)
            .AddNode(step2)
            .AddNode(step3)
            .AddNode(step4)
            .AddEdge("step1", "step2")
            .AddEdge("step2", "step3")
            .AddEdge("step3", "step4")
            .WithEntry("step1")
            .Build();

        // Act
        var result = await _executor.RunAsync(graph, new TestState(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be("step1|step2|step3|step4");
        result.Step.Should().Be(4);
    }
}

file sealed class FuncRouter<TState>(Func<TState, string> route) : IRouter<TState>
    where TState : AgentState
{
    public Task<string> DetermineNextNodeAsync(TState state, CancellationToken ct) =>
        Task.FromResult(route(state));
}
