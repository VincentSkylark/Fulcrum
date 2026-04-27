using FluentAssertions;
using Fulcrum.Core.AI;
using Xunit;

namespace Fulcrum.Core.Tests.AI;

public sealed class TaskNodeTests
{
    [Fact]
    public async Task ExecutesFuncReturnsUpdatedState()
    {
        // Arrange
        var node = new TaskNode<TestState>("test", (s, _) =>
            Task.FromResult(s with { Value = "updated", Step = s.Step + 1 }));

        // Act
        var result = await node.ExecuteAsync(new TestState("initial", 0), CancellationToken.None);

        // Assert
        result.Value.Should().Be("updated");
        result.Step.Should().Be(1);
    }

    [Fact]
    public async Task PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        CancellationToken? receivedToken = null;

        var node = new TaskNode<TestState>("test", (s, ct) =>
        {
            receivedToken = ct;
            return Task.FromResult(s);
        });

        // Act
        await node.ExecuteAsync(new TestState(), cts.Token);

        // Assert
        receivedToken.Should().Be(cts.Token);
    }

    [Fact]
    public void IdMatchesConstructorValue()
    {
        // Arrange & Act
        var node = new TaskNode<TestState>("my-node", (s, _) => Task.FromResult(s));

        // Assert
        node.Id.Should().Be("my-node");
    }
}
