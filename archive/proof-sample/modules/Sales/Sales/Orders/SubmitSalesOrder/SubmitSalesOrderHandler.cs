using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Orders.Activity;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Orders.SubmitSalesOrder;

internal sealed class SubmitSalesOrderHandler(
    SalesDbContext context,
    SalesRequestAuthorization authorization,
    TimeProvider timeProvider
)
{
    internal async Task<SubmitSalesOrderResult> HandleAsync(
        SubmitSalesOrderCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!authorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new SubmitSalesOrderResult.PermissionDenied();
        }
        if (
            !await authorization.HasPermissionAsync(
                command.ActorUserId,
                command.OrganizationId,
                SalesPermissionIds.OrdersSubmit,
                cancellationToken
            )
        )
        {
            context.AuditEntries.Add(
                SalesAuditEntry.PermissionDenied(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    SalesAuditActions.OrderSubmitDenied,
                    SalesAuditSubjectTypes.SalesOrder,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new SubmitSalesOrderResult.PermissionDenied();
        }
        if (command.ExpectedVersion <= 0)
        {
            return new SubmitSalesOrderResult.InvalidExpectedVersion();
        }
        SalesOrder? order = await context.SalesOrders.SingleOrDefaultAsync(
            order =>
                order.OrganizationId == command.OrganizationId.Value
                && order.OrderNumber == command.OrderNumber,
            cancellationToken
        );
        if (order is null)
        {
            return new SubmitSalesOrderResult.NotFound();
        }
        if (order.Version != command.ExpectedVersion)
        {
            return new SubmitSalesOrderResult.VersionConflict();
        }
        DateTimeOffset timestamp = timeProvider.GetUtcNow();
        // PostgreSQL stores microseconds; return the same timestamp that subsequent reads retain.
        DateTimeOffset now = new(timestamp.UtcTicks - timestamp.UtcTicks % 10, TimeSpan.Zero);
        if (!order.TrySubmit(command.ActorUserId.Value, now))
        {
            return new SubmitSalesOrderResult.NotDraft();
        }
        context.OrderActivity.Add(
            SalesOrderActivity.Record(
                order,
                command.ActorUserId.Value,
                SalesOrderActivityKind.Submitted,
                now
            )
        );
        context.AuditEntries.Add(
            SalesAuditEntry.Succeeded(
                command.OrganizationId.Value,
                command.ActorUserId.Value,
                SalesAuditActions.OrderSubmitted,
                SalesAuditSubjectTypes.SalesOrder,
                order.Id,
                new { order.OrderNumber, order.Version },
                now
            )
        );
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // SaveChanges rolled back its transaction. Do not leave losing activity/audit staged.
            context.ChangeTracker.Clear();
            return new SubmitSalesOrderResult.VersionConflict();
        }
        return new SubmitSalesOrderResult.Submitted(order.ToView());
    }
}
