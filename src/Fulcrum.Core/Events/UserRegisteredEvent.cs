namespace Fulcrum.Core.Events;

public sealed record UserRegisteredEvent(
    Guid KratosIdentityId,
    string Email,
    string Username,
    Guid EventId,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
