using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Access.Identity;

internal sealed class LinkExternalIdentityHandler(
    AccessDbContext context,
    TimeProvider timeProvider
) : IExternalIdentityLinking
{
    public async Task<UserIdentityLink> LinkAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(identity);

        DateTimeOffset authenticatedAt = timeProvider.GetUtcNow();
        ExternalIdentityRecord? existing = await context
            .ExternalIdentities.Include(link => link.User)
            .SingleOrDefaultAsync(
                link => link.Issuer == identity.Issuer && link.Subject == identity.Subject,
                cancellationToken
            );

        if (existing is not null)
        {
            existing.User.UpdateProfile(identity.Email, identity.DisplayName, authenticatedAt);
            existing.RecordAuthentication(authenticatedAt);
            await context.SaveChangesAsync(cancellationToken);
            return ToLink(existing.User);
        }

        var user = new User(
            Guid.CreateVersion7(authenticatedAt),
            identity.Email,
            identity.DisplayName,
            authenticatedAt
        );
        var link = new ExternalIdentityRecord(
            Guid.CreateVersion7(authenticatedAt),
            identity.Issuer,
            identity.Subject,
            user,
            authenticatedAt
        );
        context.ExternalIdentities.Add(link);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ToLink(user);
        }
        catch (DbUpdateException exception) when (IsConcurrentLink(exception))
        {
            context.ChangeTracker.Clear();
            ExternalIdentityRecord concurrent = await context
                .ExternalIdentities.Include(existingLink => existingLink.User)
                .SingleAsync(
                    existingLink =>
                        existingLink.Issuer == identity.Issuer
                        && existingLink.Subject == identity.Subject,
                    cancellationToken
                );
            return ToLink(concurrent.User);
        }
    }

    private static bool IsConcurrentLink(DbUpdateException exception) =>
        exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ExternalIdentityRecord.IssuerSubjectConstraint,
            };

    private static UserIdentityLink ToLink(User user) =>
        new(new UserId(user.Id), user.Email, user.DisplayName);
}
