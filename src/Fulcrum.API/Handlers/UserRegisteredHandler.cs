using Fulcrum.API.Data;
using Fulcrum.Core.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fulcrum.API.Handlers;

public sealed partial class UserRegisteredHandler(AppDbContext db, ILogger<UserRegisteredHandler> logger)
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Upserting profile for Kratos identity {KratosIdentityId}")]
    private static partial void LogUpserting(ILogger logger, Guid kratosIdentityId);

    public async Task HandleAsync(UserRegisteredEvent message, CancellationToken ct)
    {
        LogUpserting(logger, message.KratosIdentityId);

        var existing = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.KratosIdentityId == message.KratosIdentityId, ct);

        if (existing is not null)
        {
            existing.Email = message.Email;
            existing.Username = message.Username;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.UserProfiles.Add(new UserProfile
            {
                KratosIdentityId = message.KratosIdentityId,
                Email = message.Email,
                Username = message.Username,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
