using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Fulcrum.API.Data;
using Fulcrum.Auth.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fulcrum.Auth.Tests;

public sealed class KratosWireTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string WebhookSecret = "test-secret";
    private readonly HttpClient _client = factory.CreateClient();

    private IServiceScope CreateScope() => factory.Services.CreateScope();

    private async Task SeedProfileAsync(Guid kratosId, string email, string username, CancellationToken ct)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserProfiles.Add(new UserProfile
        {
            KratosIdentityId = kratosId,
            Email = email,
            Username = username,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Guid> RegisterViaWebhookAsync(HttpClient client, Guid kratosId, string email, string username)
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            IdentityId = kratosId.ToString(),
            Email = email,
            Username = username,
            State = "active"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/webhooks/after-registration")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Kratos-Webhook-Secret", WebhookSecret);

        var response = await client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return kratosId;
    }

    [Fact]
    public async Task WebhookAfterRegistrationCreatesProfile()
    {
        var ct = TestContext.Current.CancellationToken;
        var kratosId = Guid.NewGuid();
        await RegisterViaWebhookAsync(_client, kratosId, "new@test.com", "newuser");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.KratosIdentityId == kratosId, ct);

        profile.Should().NotBeNull();
        profile!.Email.Should().Be("new@test.com");
        profile.Username.Should().Be("newuser");
        profile.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task WebhookAfterSettingsUpdatesProfile()
    {
        var ct = TestContext.Current.CancellationToken;
        var kratosId = Guid.NewGuid();
        await SeedProfileAsync(kratosId, "old@test.com", "oldname", ct);

        var payload = new
        {
            IdentityId = kratosId.ToString(),
            Email = "updated@test.com",
            Username = "newname",
            State = "active"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/webhooks/after-settings")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Kratos-Webhook-Secret", WebhookSecret);

        var response = await _client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.FirstAsync(p => p.KratosIdentityId == kratosId, ct);

        profile.Email.Should().Be("updated@test.com");
        profile.Username.Should().Be("newname");
    }

    [Fact]
    public async Task WebhookInvalidSecretReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            IdentityId = Guid.NewGuid().ToString(),
            Email = "test@test.com",
            Username = "testuser",
            State = "active"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/webhooks/after-registration")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Kratos-Webhook-Secret", "wrong-secret");

        var response = await _client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginValidCredentialsReturnsAuthResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var kratosId = Guid.NewGuid();
        factory.Kratos.AddIdentity(kratosId, "login@test.com", "loginuser");
        await SeedProfileAsync(kratosId, "login@test.com", "loginuser", ct);

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Identifier = "login@test.com", Password = "valid-password" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponseStub>(ct);
        auth.Should().NotBeNull();
        auth!.Email.Should().Be("login@test.com");
        auth.Username.Should().Be("loginuser");
    }

    [Fact]
    public async Task LoginValidCredentialsUpdatesLastLoginAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var kratosId = Guid.NewGuid();
        factory.Kratos.AddIdentity(kratosId, "lastlogin@test.com", "lastloginuser");
        await SeedProfileAsync(kratosId, "lastlogin@test.com", "lastloginuser", ct);

        var before = DateTimeOffset.UtcNow;

        await _client.PostAsJsonAsync("/api/auth/login",
            new { Identifier = "lastlogin@test.com", Password = "valid-password" }, ct);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.FirstAsync(p => p.KratosIdentityId == kratosId, ct);

        profile.LastLoginAt.Should().NotBeNull();
        profile.LastLoginAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task DeleteAccountSoftDeletesProfile()
    {
        var ct = TestContext.Current.CancellationToken;
        var kratosId = Guid.NewGuid();
        var sessionToken = $"session-{kratosId}";
        factory.Kratos.AddIdentity(kratosId, "delete@test.com", "deleteuser");
        await SeedProfileAsync(kratosId, "delete@test.com", "deleteuser", ct);
        factory.Kratos.MapSession($"ory_kratos_session={sessionToken}", kratosId);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account");
        request.Headers.Add("Cookie", $"ory_kratos_session={sessionToken}");

        var response = await _client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.FirstAsync(p => p.KratosIdentityId == kratosId, ct);

        profile.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProfileGetReturnsProfile()
    {
        var ct = TestContext.Current.CancellationToken;
        var kratosId = Guid.NewGuid();
        var sessionToken = $"session-{kratosId}";
        factory.Kratos.AddIdentity(kratosId, "profile@test.com", "profileuser");
        await SeedProfileAsync(kratosId, "profile@test.com", "profileuser", ct);
        factory.Kratos.MapSession($"ory_kratos_session={sessionToken}", kratosId);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/profile");
        request.Headers.Add("Cookie", $"ory_kratos_session={sessionToken}");

        var response = await _client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<ProfileStub>(ct);
        profile.Should().NotBeNull();
        profile!.Username.Should().Be("profileuser");
        profile.Email.Should().Be("profile@test.com");
    }

    [Fact]
    public async Task ProfileUpdateSyncsToKratosAndDb()
    {
        var ct = TestContext.Current.CancellationToken;
        var kratosId = Guid.NewGuid();
        var sessionToken = $"session-{kratosId}";
        factory.Kratos.AddIdentity(kratosId, "sync@test.com", "syncuser");
        await SeedProfileAsync(kratosId, "sync@test.com", "syncuser", ct);
        factory.Kratos.MapSession($"ory_kratos_session={sessionToken}", kratosId);

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/profile")
        {
            Content = JsonContent.Create(new { Email = "synced@test.com", Username = "synceduser" })
        };
        request.Headers.Add("Cookie", $"ory_kratos_session={sessionToken}");

        var response = await _client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.FirstAsync(p => p.KratosIdentityId == kratosId, ct);
        profile.Email.Should().Be("synced@test.com");
        profile.Username.Should().Be("synceduser");

        factory.Kratos.VerifyIdentity(kratosId, "synced@test.com", "synceduser");
    }

    private sealed record AuthResponseStub(Guid UserId, string Email, string? Username);
    private sealed record ProfileStub(Guid Id, string Email, string Username, string? AvatarUrl, string Tier, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? DeletedAt, DateTimeOffset? LastLoginAt);
}
