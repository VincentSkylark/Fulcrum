namespace Fulcrum.Core.Events;

public sealed record UserLoggedInEvent(
    Guid KratosIdentityId,
    Guid EventId,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
