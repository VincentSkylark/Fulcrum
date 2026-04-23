using Fulcrum.Core.Errors;

namespace Fulcrum.Auth.Clients;

public interface IKratosClient
{
    Task<Result<SessionInfo>> ValidateSessionAsync(string cookieOrToken, CancellationToken ct);
    Task<Result<SessionInfo>> SubmitLoginFlowAsync(string identifier, string password, CancellationToken ct);
    Task<Result<SessionInfo>> SubmitRegistrationFlowAsync(string email, string password, string username, CancellationToken ct);
    Task<Result> SubmitRecoveryFlowAsync(string email, CancellationToken ct);
    Task<Result> VerifyEmailAsync(string flowId, string code, CancellationToken ct);
    Task<Result> DeleteIdentityAsync(Guid identityId, CancellationToken ct);
    Task<Result> UpdateIdentityTraitsAsync(Guid identityId, string email, string username, CancellationToken ct);
}
