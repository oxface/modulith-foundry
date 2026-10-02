using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Fulfilment;
using ModulithFoundry.Modules.Sales.Orders.Activity;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Orders.CancelSalesOrder;

internal sealed class CancelSalesOrderHandler(
    SalesDbContext context,
    SalesRequestAuthorization authorization,
    TimeProvider clock
) : ISalesOrderCancellation
{
    public async Task<CancelSalesOrderResult> CancelAsync(
        CancelSalesOrderCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!authorization.MatchesContext(command.ActorUserId, command.OrganizationId))
            return new CancelSalesOrderResult.PermissionDenied();
        if (
            !await authorization.HasPermissionAsync(
                command.ActorUserId,
                command.OrganizationId,
                SalesPermissionIds.OrdersCancel,
                cancellationToken
            )
        )
        {
            context.AuditEntries.Add(
                SalesAuditEntry.PermissionDenied(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    SalesAuditActions.OrderCancelDenied,
                    SalesAuditSubjectTypes.SalesOrder,
                    clock.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new CancelSalesOrderResult.PermissionDenied();
        }
        if (command.ExpectedVersion <= 0)
            return new CancelSalesOrderResult.InvalidExpectedVersion();
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        var order = await context.SalesOrders.SingleOrDefaultAsync(
            item => item.OrderNumber == command.OrderNumber,
            cancellationToken
        );
        if (order is null)
            return new CancelSalesOrderResult.NotFound();
        // Lost-response retries retain the first cancellation, including its reason and actor.
        if (order.Status == SalesOrderStatus.Cancelled)
            return new CancelSalesOrderResult.Unchanged(order.ToView());
        if (order.Version != command.ExpectedVersion)
            return new CancelSalesOrderResult.VersionConflict();
        var process = await context.FulfilmentProcesses.SingleOrDefaultAsync(
            item => item.OrderId == order.Id,
            cancellationToken
        );
        if (order.Status == SalesOrderStatus.Approved && process is null)
            throw new FulfilmentIntegrityException(order.Id);
        var time = clock.GetUtcNow();
        DateTimeOffset now = new(time.UtcTicks - time.UtcTicks % 10, TimeSpan.Zero);
        try
        {
            order.TryCancel(command.ActorUserId.Value, command.Reason, now);
        }
        catch (InvalidSalesOrderInputException)
        {
            return new CancelSalesOrderResult.InvalidReason();
        }
        if (process is not null)
        {
            var previousStatus = process.Status;
            process.RequestCancellation();
            foreach (var line in process.Lines)
                ReleaseCommandStaging.Stage(context, process, line, now);
            CompensationActivity.StageCompletion(
                context,
                process,
                previousStatus,
                order.Version,
                now
            );
        }
        context.OrderActivity.Add(
            SalesOrderActivity.Record(
                order,
                command.ActorUserId.Value,
                SalesOrderActivityKind.Cancelled,
                now
            )
        );
        context.AuditEntries.Add(
            SalesAuditEntry.Succeeded(
                command.OrganizationId.Value,
                command.ActorUserId.Value,
                SalesAuditActions.OrderCancelled,
                SalesAuditSubjectTypes.SalesOrder,
                order.Id,
                new
                {
                    order.OrderNumber,
                    order.Version,
                    ProcessId = process?.Id,
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
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            return new CancelSalesOrderResult.VersionConflict();
        }
        return new CancelSalesOrderResult.Cancelled(order.ToView());
    }
}
