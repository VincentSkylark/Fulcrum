using Fulcrum.Auth.Clients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fulcrum.Auth.Endpoints;

public static class RecoveryEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/recovery", async (RecoveryRequest request, IKratosClient kratos, CancellationToken ct) =>
        {
            var result = await kratos.SubmitRecoveryFlowAsync(request.Email, ct);

            return result.IsSuccess
                ? Results.Ok(new { message = "Recovery email sent" })
                : Results.Problem(statusCode: 400, title: "Recovery failed", detail: result.Error.Message);
        });
    }
}
