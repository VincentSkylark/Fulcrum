using FluentAssertions;
using Fulcrum.Auth.Clients;
using Fulcrum.Core.Errors;

namespace Fulcrum.Auth.Tests.Infrastructure;

public sealed class MockKratosClient : IKratosClient
{
    private readonly Dictionary<Guid, (string Email, string Username)> _identities = [];
    private readonly Dictionary<string, Guid> _sessions = [];

    public void AddIdentity(Guid id, string email, string username)
        => _identities[id] = (email, username);

    public void MapSession(string token, Guid identityId)
        => _sessions[token] = identityId;

    public Task<Result<SessionInfo>> ValidateSessionAsync(string cookieOrToken, CancellationToken ct)
    {
        if (_sessions.TryGetValue(cookieOrToken, out var identityId) &&
            _identities.TryGetValue(identityId, out var identity))
        {
            return Task.FromResult(Result<SessionInfo>.Success(new SessionInfo(
                identityId, identity.Email, identity.Username, DateTimeOffset.UtcNow.AddHours(1))));
        }

        return Task.FromResult(Result<SessionInfo>.Failure(
            Error.Unauthorized("auth.session_invalid", "Session is invalid or expired")));
    }

    public Task<Result<SessionInfo>> SubmitLoginFlowAsync(string identifier, string password, CancellationToken ct)
    {
        var match = _identities.FirstOrDefault(kvp =>
            kvp.Value.Email.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            kvp.Value.Username.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        if (match.Key != Guid.Empty && password == "valid-password")
        {
            return Task.FromResult(Result<SessionInfo>.Success(new SessionInfo(
                match.Key, match.Value.Email, match.Value.Username, DateTimeOffset.UtcNow.AddHours(1))));
        }

        return Task.FromResult(Result<SessionInfo>.Failure(
            Error.Validation("auth.login_failed", "Invalid credentials")));
    }

    public Task<Result<SessionInfo>> SubmitRegistrationFlowAsync(string email, string password, string username, CancellationToken ct)
    {
        if (_identities.Values.Any(v => v.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(Result<SessionInfo>.Failure(
                Error.Validation("auth.registration_failed", "Email already exists")));

        if (_identities.Values.Any(v => v.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(Result<SessionInfo>.Failure(
                Error.Validation("auth.registration_failed", "Username already exists")));

        var id = Guid.NewGuid();
        _identities[id] = (email, username);

        return Task.FromResult(Result<SessionInfo>.Success(new SessionInfo(
            id, email, username, DateTimeOffset.UtcNow.AddHours(1))));
    }

    public Task<Result> SubmitRecoveryFlowAsync(string email, CancellationToken ct)
        => Task.FromResult(Result.Success());

    public Task<Result> VerifyEmailAsync(string flowId, string code, CancellationToken ct)
        => Task.FromResult(Result.Success());

    public Task<Result> DeleteIdentityAsync(Guid identityId, CancellationToken ct)
    {
        _identities.Remove(identityId);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> UpdateIdentityTraitsAsync(Guid identityId, string email, string username, CancellationToken ct)
    {
        if (_identities.TryGetValue(identityId, out var existing))
        {
            _identities[identityId] = (email, username);
            return Task.FromResult(Result.Success());
        }

        return Task.FromResult(Result.Failure(
            Error.Unexpected("auth.update_failed", $"Identity {identityId} not found")));
    }

    public void VerifyIdentity(Guid id, string expectedEmail, string expectedUsername)
    {
        _identities.Should().ContainKey(id);
        _identities[id].Email.Should().Be(expectedEmail);
        _identities[id].Username.Should().Be(expectedUsername);
    }
}
