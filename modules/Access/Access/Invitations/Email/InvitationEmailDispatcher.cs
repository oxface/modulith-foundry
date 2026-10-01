using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Access.Email;
using ModulithFoundry.Modules.Access.ExtensionPoints;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Invitations.Email;

internal sealed partial class InvitationEmailDispatcher(
    AccessDbContext context,
    TimeProvider timeProvider,
    InvitationEmailPayloadCodec payloadCodec,
    IEmailTransport emailTransport,
    ILogger<InvitationEmailDispatcher> logger
)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    internal async Task<bool> DispatchNextAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? deliveryId = await context
            .InvitationEmailDeliveries.AsNoTracking()
            .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .Where(delivery => delivery.SentAt == null)
            .Where(delivery => delivery.SupersededAt == null)
            .Where(delivery => delivery.ProtectedPayload != null)
            .Where(delivery => delivery.AvailableAt <= now)
            .Where(delivery => delivery.LeaseExpiresAt == null || delivery.LeaseExpiresAt <= now)
            .OrderBy(delivery => delivery.AvailableAt)
            .ThenBy(delivery => delivery.Id)
            .Select(delivery => (Guid?)delivery.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (deliveryId is null)
        {
            return false;
        }

        Guid leaseId = Guid.CreateVersion7(now);
        int claimed = await context
            .InvitationEmailDeliveries.IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .Where(delivery => delivery.Id == deliveryId.Value)
            .Where(delivery => delivery.SentAt == null)
            .Where(delivery => delivery.SupersededAt == null)
            .Where(delivery => delivery.ProtectedPayload != null)
            .Where(delivery => delivery.AvailableAt <= now)
            .Where(delivery => delivery.LeaseExpiresAt == null || delivery.LeaseExpiresAt <= now)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(delivery => delivery.LeaseId, leaseId)
                        .SetProperty(delivery => delivery.LeaseExpiresAt, now.Add(LeaseDuration)),
                cancellationToken
            );
        if (claimed == 0)
        {
            return true;
        }

        InvitationEmailDelivery delivery = await context
            .InvitationEmailDeliveries.IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .SingleAsync(
                candidate => candidate.Id == deliveryId.Value && candidate.LeaseId == leaseId,
                cancellationToken
            );

        try
        {
            string protectedPayload =
                delivery.ProtectedPayload
                ?? throw new InvalidOperationException("Claimed invitation email has no payload.");
            InvitationEmailPayload payload = payloadCodec.Unprotect(protectedPayload);
            await emailTransport.SendAsync(
                InvitationEmailRenderer.Render(payload),
                cancellationToken
            );
            delivery.MarkSent(timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            DateTimeOffset failedAt = timeProvider.GetUtcNow();
            delivery.ReleaseAfterFailure(failedAt.Add(RetryDelay(delivery.AttemptCount)));
            await context.SaveChangesAsync(cancellationToken);
            LogDeliveryFailure(logger, delivery.Id, delivery.AttemptCount, exception);
        }

        return true;
    }

    private static TimeSpan RetryDelay(int attemptCount) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Min(attemptCount, 8)), 300));

    [LoggerMessage(
        LogLevel.Warning,
        "Invitation email delivery {DeliveryId} failed on attempt {AttemptCount}; it remains pending."
    )]
    private static partial void LogDeliveryFailure(
        ILogger logger,
        Guid deliveryId,
        int attemptCount,
        Exception exception
    );
}
