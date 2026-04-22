namespace Fulcrum.Core.Identity;

public interface ICurrentUserAccessor
{
    Guid UserId { get; }
    string Email { get; }
    string Tier { get; }
    bool IsAuthenticated { get; }
}
