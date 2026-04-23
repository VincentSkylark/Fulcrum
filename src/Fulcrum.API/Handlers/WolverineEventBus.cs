using Fulcrum.Core.Events;
using Wolverine;

namespace Fulcrum.API.Handlers;

public sealed class WolverineEventBus(IMessageBus bus) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
        => await bus.PublishAsync(@event);
}
