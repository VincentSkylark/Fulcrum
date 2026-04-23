using Fulcrum.Auth.Clients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fulcrum.Auth.Endpoints;

public static class RegisterEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", async (RegisterRequest request, IKratosClient kratos, CancellationToken ct) =>
        {
            var result = await kratos.SubmitRegistrationFlowAsync(
                request.Email, request.Password, request.Username, ct);

            return result.IsSuccess && result.Value is not null
                ? Results.Ok(new AuthResponse(result.Value.IdentityId, result.Value.Email, result.Value.Username))
                : Results.Problem(statusCode: 400, title: "Registration failed", detail: result.Error.Message);
        });
    }
}
