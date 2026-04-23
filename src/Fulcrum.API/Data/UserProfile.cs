using Fulcrum.Core.Domain;

namespace Fulcrum.API.Data;

public sealed class UserProfile : Entity<Guid>
{
    public UserProfile() : base(Guid.NewGuid()) { }

    public UserProfile(Guid id) : base(id) { }

    public Guid KratosIdentityId { get; init; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Tier { get; set; } = "free";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
