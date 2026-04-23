using System.Net.Http.Json;
using Fulcrum.Auth.Configuration;
using Fulcrum.Core.Errors;
using Microsoft.Extensions.Options;

namespace Fulcrum.Auth.Clients;

public sealed class KratosClient(IOptions<KratosOptions> options) : IKratosClient, IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri(options.Value.AdminBaseUrl.TrimEnd('/')) };

    public async Task<Result<SessionInfo>> ValidateSessionAsync(string cookieOrToken, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/sessions/whoami");
            request.Headers.Add("Cookie", cookieOrToken);
            if (!cookieOrToken.Contains('='))
                request.Headers.Add("X-Session-Token", cookieOrToken);

            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return Result<SessionInfo>.Failure(Error.Unauthorized("auth.session_invalid", "Session is invalid or expired"));

            var session = await response.Content.ReadFromJsonAsync<KratosSessionResponse>(ct);
            return session is null
                ? Result<SessionInfo>.Failure(Error.Unexpected("auth.session_parse_error", "Failed to parse session"))
                : MapSession(session);
        }
        catch (HttpRequestException)
        {
            return Result<SessionInfo>.Failure(Error.Unexpected("auth.kratos_unavailable", "Unable to reach identity service"));
        }
    }

    public async Task<Result<SessionInfo>> SubmitLoginFlowAsync(string identifier, string password, CancellationToken ct)
    {
        try
        {
            var flowResponse = await _http.PostAsync("/self-service/login/api", null, ct);
            var flow = await flowResponse.Content.ReadFromJsonAsync<KratosFlowResponse>(ct);
            if (flow is null)
                return Result<SessionInfo>.Failure(Error.Unexpected("auth.flow_error", "Failed to create login flow"));

            var body = new
            {
                method = "password",
                identifier,
                password
            };

            var submitResponse = await _http.PostAsJsonAsync($"/self-service/login?flow={flow.Id}", body, ct);
            if (!submitResponse.IsSuccessStatusCode)
            {
                var error = await submitResponse.Content.ReadAsStringAsync(ct);
                return Result<SessionInfo>.Failure(Error.Validation("auth.login_failed", ExtractErrorMessage(error)));
            }

            var result = await submitResponse.Content.ReadFromJsonAsync<KratosLoginResponse>(ct);
            return result?.Session is null
                ? Result<SessionInfo>.Failure(Error.Unexpected("auth.login_error", "Login succeeded but session was empty"))
                : MapSession(result.Session);
        }
        catch (HttpRequestException ex)
        {
            return Result<SessionInfo>.Failure(Error.Unexpected("auth.kratos_unavailable", ex.Message));
        }
    }

    public async Task<Result<SessionInfo>> SubmitRegistrationFlowAsync(string email, string password, string username, CancellationToken ct)
    {
        try
        {
            var flowResponse = await _http.PostAsync("/self-service/registration/api", null, ct);
            var flow = await flowResponse.Content.ReadFromJsonAsync<KratosFlowResponse>(ct);
            if (flow is null)
                return Result<SessionInfo>.Failure(Error.Unexpected("auth.flow_error", "Failed to create registration flow"));

            var body = new
            {
                method = "password",
                password,
                traits = new { email, username }
            };

            var submitResponse = await _http.PostAsJsonAsync($"/self-service/registration?flow={flow.Id}", body, ct);
            if (!submitResponse.IsSuccessStatusCode)
            {
                var error = await submitResponse.Content.ReadAsStringAsync(ct);
                return Result<SessionInfo>.Failure(Error.Validation("auth.registration_failed", ExtractErrorMessage(error)));
            }

            var result = await submitResponse.Content.ReadFromJsonAsync<KratosRegistrationResponse>(ct);
            return result?.Session is null
                ? Result<SessionInfo>.Failure(Error.Unexpected("auth.registration_error", "Registration succeeded but session was empty"))
                : MapSession(result.Session);
        }
        catch (HttpRequestException ex)
        {
            return Result<SessionInfo>.Failure(Error.Unexpected("auth.kratos_unavailable", ex.Message));
        }
    }

    public async Task<Result> SubmitRecoveryFlowAsync(string email, CancellationToken ct)
    {
        try
        {
            var flowResponse = await _http.PostAsync("/self-service/recovery/api", null, ct);
            var flow = await flowResponse.Content.ReadFromJsonAsync<KratosFlowResponse>(ct);
            if (flow is null)
                return Result.Failure(Error.Unexpected("auth.flow_error", "Failed to create recovery flow"));

            var body = new { method = "code", email };
            var submitResponse = await _http.PostAsJsonAsync($"/self-service/recovery?flow={flow.Id}", body, ct);

            return submitResponse.IsSuccessStatusCode
                ? Result.Success()
                : Result.Failure(Error.Validation("auth.recovery_failed", "Recovery request failed"));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure(Error.Unexpected("auth.kratos_unavailable", ex.Message));
        }
    }

    public async Task<Result> VerifyEmailAsync(string flowId, string code, CancellationToken ct)
    {
        try
        {
            var body = new { method = "code", code };
            var response = await _http.PostAsJsonAsync($"/self-service/verification?flow={flowId}", body, ct);

            return response.IsSuccessStatusCode
                ? Result.Success()
                : Result.Failure(Error.Validation("auth.verification_failed", "Email verification failed"));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure(Error.Unexpected("auth.kratos_unavailable", ex.Message));
        }
    }

    public async Task<Result> DeleteIdentityAsync(Guid identityId, CancellationToken ct)
    {
        try
        {
            var response = await _http.DeleteAsync($"/admin/identities/{identityId}", ct);

            return response.IsSuccessStatusCode
                ? Result.Success()
                : Result.Failure(Error.Unexpected("auth.delete_failed", $"Failed to delete identity {identityId}"));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure(Error.Unexpected("auth.kratos_unavailable", ex.Message));
        }
    }

    public async Task<Result> UpdateIdentityTraitsAsync(Guid identityId, string email, string username, CancellationToken ct)
    {
        try
        {
            var body = new { traits = new { email, username } };
            var response = await _http.PatchAsJsonAsync($"/admin/identities/{identityId}", body, ct);

            return response.IsSuccessStatusCode
                ? Result.Success()
                : Result.Failure(Error.Unexpected("auth.update_failed", $"Failed to update identity {identityId}"));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure(Error.Unexpected("auth.kratos_unavailable", ex.Message));
        }
    }

    public void Dispose() => _http.Dispose();

    private static SessionInfo MapSession(KratosSessionResponse session)
    {
        var identity = session.Identity;
        if (identity is null)
            return new SessionInfo(Guid.Empty, string.Empty, null, DateTimeOffset.MinValue);

        var traits = identity.Traits;

        return new SessionInfo(
            Guid.TryParse(identity.Id, out var id) ? id : Guid.Empty,
            traits?.GetValueOrDefault("email")?.ToString() ?? string.Empty,
            traits?.GetValueOrDefault("username")?.ToString(),
            session.ExpiresAt);
    }

    private static string ExtractErrorMessage(string responseContent)
    {
        try
        {
            var error = System.Text.Json.JsonDocument.Parse(responseContent);
            if (error.RootElement.TryGetProperty("ui", out var ui) &&
                ui.TryGetProperty("messages", out var messages))
            {
                foreach (var msg in messages.EnumerateArray())
                {
                    if (msg.TryGetProperty("text", out var text))
                        return text.GetString() ?? responseContent;
                }
            }

            return responseContent;
        }
        catch
        {
            return responseContent;
        }
    }

    private sealed record KratosFlowResponse(string Id);
    private sealed record KratosIdentityResponse(string Id, Dictionary<string, object>? Traits);
    private sealed record KratosSessionResponse(KratosIdentityResponse? Identity, DateTimeOffset ExpiresAt);
    private sealed record KratosLoginResponse(KratosSessionResponse? Session);
    private sealed record KratosRegistrationResponse(KratosSessionResponse? Session);
}
