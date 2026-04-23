namespace Fulcrum.Core.Events;

public sealed record UserDeletedEvent(
    Guid KratosIdentityId,
    Guid EventId,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
