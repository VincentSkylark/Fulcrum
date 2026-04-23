using Fulcrum.API.Handlers;
using Fulcrum.Core.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Fulcrum.Auth.Tests.Infrastructure;

public sealed class SynchronousEventBus(IServiceProvider sp) : IEventBus
{
    private static readonly Dictionary<Type, Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>> Handlers = new()
    {
        [typeof(UserRegisteredEvent)] = (sp, e, ct) =>
            sp.GetRequiredService<UserRegisteredHandler>().HandleAsync((UserRegisteredEvent)e, ct),
        [typeof(UserLoggedInEvent)] = (sp, e, ct) =>
            sp.GetRequiredService<UserLoggedInHandler>().HandleAsync((UserLoggedInEvent)e, ct),
        [typeof(UserDeletedEvent)] = (sp, e, ct) =>
            sp.GetRequiredService<UserDeletedHandler>().HandleAsync((UserDeletedEvent)e, ct),
    };

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IIntegrationEvent
    {
        if (!Handlers.TryGetValue(typeof(TEvent), out var handler))
            throw new InvalidOperationException($"No test handler registered for event type {typeof(TEvent).Name}. Add it to {nameof(SynchronousEventBus)}.{nameof(Handlers)}.");

        using var scope = sp.CreateScope();
        await handler(scope.ServiceProvider, @event, ct);
    }
}
