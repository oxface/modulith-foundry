using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations.Queries;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Organizations;

internal sealed class OrganizationAuthorizationGuard(
    AccessDbContext context,
    IOrganizationContextAccessor contextAccessor,
    OrganizationMembershipQueries queries
) : IOrganizationAuthorizationGuard
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(
        UserId userId,
        OrganizationId organizationId,
        MembershipId membershipId,
        string permissionId,
        CancellationToken cancellationToken = default
    )
    {
        if (
            contextAccessor.OrganizationContext is not { } actor
            || actor.UserId != userId
            || actor.OrganizationId != organizationId
            || actor.MembershipId != membershipId
        )
            return null;
        IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken
        );
        bool handedOff = false;
        try
        {
            bool exists = await context
                .Organizations.FromSqlInterpolated(
                    $$"""
                    SELECT id, name, slug, created_at FROM access.organizations
                    WHERE id = {{organizationId.Value}} FOR SHARE
                    """
                )
                .AsNoTracking()
                .AnyAsync(cancellationToken);
            if (
                !exists
                || !await context
                    .Memberships.AsNoTracking()
                    .Active()
                    .AnyAsync(
                        member =>
                            member.OrganizationId == organizationId.Value
                            && member.Id == membershipId.Value
                            && member.UserId == userId.Value,
                        cancellationToken
                    )
                || !await queries.HasPermissionAsync(
                    userId,
                    organizationId,
                    permissionId,
                    cancellationToken
                )
            )
                return null;
            handedOff = true;
            return new Guard(transaction);
        }
        finally
        {
            if (!handedOff)
                await transaction.DisposeAsync();
        }
    }

    private sealed class Guard(IDbContextTransaction transaction) : IAsyncDisposable
    {
        // This Access transaction is read-only. Disposal rolls it back and releases the guard.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
