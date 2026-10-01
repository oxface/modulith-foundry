using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.ApprovalAuthorities;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Fulfilment;
using ModulithFoundry.Modules.Sales.Fulfilment.Persistence;
using ModulithFoundry.Modules.Sales.Orders.Activity;
using ModulithFoundry.Modules.Sales.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Sales.Orders.ApproveSalesOrder;

internal sealed class ApproveSalesOrderHandler(
    SalesDbContext context,
    SalesRequestAuthorization authorization,
    IOrganizationContextAccessor contextAccessor,
    IOrganizationAuthorizationGuard authorizationGuard,
    TimeProvider timeProvider
) : ISalesOrderApproval
{
    public async Task<ApproveSalesOrderResult> ApproveAsync(
        ApproveSalesOrderCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!authorization.MatchesContext(command.ActorUserId, command.OrganizationId))
            return new ApproveSalesOrderResult.PermissionDenied();
        MembershipId membershipId = contextAccessor.OrganizationContext!.MembershipId;
        await using IAsyncDisposable? guard = await authorizationGuard.TryAcquireAsync(
            command.ActorUserId,
            command.OrganizationId,
            membershipId,
            SalesPermissionIds.OrdersApprove,
            cancellationToken
        );
        if (guard is null)
        {
            context.AuditEntries.Add(
                SalesAuditEntry.PermissionDenied(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    SalesAuditActions.OrderApproveDenied,
                    SalesAuditSubjectTypes.SalesOrder,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new ApproveSalesOrderResult.PermissionDenied();
        }
        if (command.ExpectedVersion <= 0)
            return new ApproveSalesOrderResult.InvalidExpectedVersion();
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        SalesOrder? order = await context.SalesOrders.SingleOrDefaultAsync(
            entity =>
                entity.OrganizationId == command.OrganizationId.Value
                && entity.OrderNumber == command.OrderNumber,
            cancellationToken
        );
        if (order is null)
            return new ApproveSalesOrderResult.NotFound();
        if (order.Version != command.ExpectedVersion)
            return new ApproveSalesOrderResult.VersionConflict();
        if (order.Status != SalesOrderStatus.AwaitingApproval)
            return new ApproveSalesOrderResult.NotAwaitingApproval();
        SalesApprovalAuthority? authority = await context
            .ApprovalAuthorities.FromSqlInterpolated(
                $$"""
                SELECT id, organization_id, membership_id, maximum_amount, currency, is_enabled, version
                FROM sales.approval_authorities
                WHERE organization_id = {{command.OrganizationId.Value}} AND membership_id = {{membershipId.Value}}
                FOR SHARE
                """
            )
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (
            OrderApprovalPolicy.Evaluate(order, command.ActorUserId.Value, authority) is
            { } rejection
        )
        {
            (ApproveSalesOrderResult result, string reasonCode) = rejection switch
            {
                OrderApprovalRejection.SelfApproval => (
                    (ApproveSalesOrderResult)new ApproveSalesOrderResult.SelfApprovalDenied(),
                    SalesAuditReasonCodes.SelfApproval
                ),
                OrderApprovalRejection.AuthorityUnavailable => (
                    new ApproveSalesOrderResult.AuthorityUnavailable(),
                    SalesAuditReasonCodes.ApprovalAuthorityUnavailable
                ),
                OrderApprovalRejection.CurrencyMismatch => (
                    new ApproveSalesOrderResult.CurrencyMismatch(),
                    SalesAuditReasonCodes.ApprovalCurrencyMismatch
                ),
                OrderApprovalRejection.LimitExceeded => (
                    new ApproveSalesOrderResult.LimitExceeded(),
                    SalesAuditReasonCodes.ApprovalLimitExceeded
                ),
                _ => throw new UnreachableException(),
            };
            context.AuditEntries.Add(
                SalesAuditEntry.Denied(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    SalesAuditActions.OrderApproveDenied,
                    SalesAuditSubjectTypes.SalesOrder,
                    order.Id,
                    reasonCode,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        SalesApprovalAuthority acceptedAuthority = authority ?? throw new UnreachableException();
        DateTimeOffset timestamp = timeProvider.GetUtcNow();
        DateTimeOffset now = new(timestamp.UtcTicks - timestamp.UtcTicks % 10, TimeSpan.Zero);
        if (!order.TryApprove(command.ActorUserId.Value, now))
            throw new UnreachableException();
        context.FulfilmentProcesses.Add(OrderFulfilmentProcess.Start(order, now));
        context.OrderActivity.Add(
            SalesOrderActivity.Record(
                order,
                command.ActorUserId.Value,
                SalesOrderActivityKind.Approved,
                now
            )
        );
        context.AuditEntries.Add(
            SalesAuditEntry.Succeeded(
                command.OrganizationId.Value,
                command.ActorUserId.Value,
                SalesAuditActions.OrderApproved,
                SalesAuditSubjectTypes.SalesOrder,
                order.Id,
                new
                {
                    order.OrderNumber,
                    order.Version,
                    MembershipId = membershipId.Value,
                    AuthorityVersion = acceptedAuthority.Version,
                    acceptedAuthority.MaximumAmount,
                    acceptedAuthority.Currency,
                },
                now
            )
        );
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return new ApproveSalesOrderResult.VersionConflict();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException
                    is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: OrderFulfilmentProcessConfiguration.OrderConstraint
                    }
            )
        {
            context.ChangeTracker.Clear();
            return new ApproveSalesOrderResult.VersionConflict();
        }
        return new ApproveSalesOrderResult.Approved(order.ToView());
    }
}
