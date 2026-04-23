using Fulcrum.Auth.Clients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fulcrum.Auth.Endpoints;

public static class VerificationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/verify", async (VerifyRequest request, IKratosClient kratos, CancellationToken ct) =>
        {
            var result = await kratos.VerifyEmailAsync(request.FlowId, request.Code, ct);

            return result.IsSuccess
                ? Results.Ok(new { message = "Email verified" })
                : Results.Problem(statusCode: 400, title: "Verification failed", detail: result.Error.Message);
        });
    }
}
