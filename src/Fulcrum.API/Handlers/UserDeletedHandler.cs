using Fulcrum.API.Data;
using Fulcrum.Core.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fulcrum.API.Handlers;

public sealed partial class UserDeletedHandler(AppDbContext db, ILogger<UserDeletedHandler> logger)
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Soft-deleting profile for Kratos identity {KratosIdentityId}")]
    private static partial void LogDeleting(ILogger logger, Guid kratosIdentityId);

    public async Task HandleAsync(UserDeletedEvent message, CancellationToken ct)
    {
        LogDeleting(logger, message.KratosIdentityId);

        var profile = await db.UserProfiles
            .FirstOrDefaultAsync(p => p.KratosIdentityId == message.KratosIdentityId, ct);

        if (profile is not null)
        {
            profile.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
