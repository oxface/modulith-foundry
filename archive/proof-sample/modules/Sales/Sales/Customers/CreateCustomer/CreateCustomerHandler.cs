using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Customers.Persistence;
using ModulithFoundry.Modules.Sales.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Sales.Customers.CreateCustomer;

internal sealed class CreateCustomerHandler(
    SalesDbContext context,
    SalesRequestAuthorization authorization,
    TimeProvider timeProvider
)
{
    internal async Task<CreateCustomerResult> HandleAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!authorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new CreateCustomerResult.PermissionDenied();
        }
        if (
            !await authorization.HasPermissionAsync(
                command.ActorUserId,
                command.OrganizationId,
                SalesPermissionIds.CustomersManage,
                cancellationToken
            )
        )
        {
            context.AuditEntries.Add(
                SalesAuditEntry.PermissionDenied(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    SalesAuditActions.CustomerCreateDenied,
                    SalesAuditSubjectTypes.Customer,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new CreateCustomerResult.PermissionDenied();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Customer customer;
        try
        {
            customer = Customer.Create(
                Guid.CreateVersion7(now),
                command.OrganizationId.Value,
                command.Code,
                command.Name,
                now
            );
        }
        catch (InvalidCustomerInputException exception)
        {
            return new CreateCustomerResult.Invalid(exception.Field, exception.Message);
        }

        context.Customers.Add(customer);
        SalesAuditEntry audit = SalesAuditEntry.Succeeded(
            customer.OrganizationId,
            command.ActorUserId.Value,
            SalesAuditActions.CustomerCreated,
            SalesAuditSubjectTypes.Customer,
            customer.Id,
            new { customer.Code },
            now
        );
        context.AuditEntries.Add(audit);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException
                    is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: CustomerConfiguration.OrganizationCodeConstraint,
                    }
            )
        {
            // SaveChanges rolled back both inserts; don't leave rejected changes for the next call.
            context.Entry(customer).State = EntityState.Detached;
            context.Entry(audit).State = EntityState.Detached;
            return new CreateCustomerResult.CodeUnavailable(customer.Code);
        }
        return new CreateCustomerResult.Created(customer.ToView());
    }
}
