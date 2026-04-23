using Fulcrum.API.Data;
using Fulcrum.Core.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fulcrum.API.Handlers;

public sealed partial class UserLoggedInHandler(AppDbContext db, ILogger<UserLoggedInHandler> logger)
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Updating LastLoginAt for Kratos identity {KratosIdentityId}")]
    private static partial void LogLoginUpdate(ILogger logger, Guid kratosIdentityId);

    public async Task HandleAsync(UserLoggedInEvent message, CancellationToken ct)
    {
        LogLoginUpdate(logger, message.KratosIdentityId);

        var profile = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.KratosIdentityId == message.KratosIdentityId, ct);

        if (profile is not null)
        {
            profile.LastLoginAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
