using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Customers.Queries;

internal sealed class CustomerQueries(SalesDbContext context, SalesRequestAuthorization authorization)
{
    internal async Task<GetCustomerResult> GetByCodeAsync(
        UserId actorUserId, OrganizationId organizationId, string code, CancellationToken cancellationToken)
    {
        if (!await authorization.HasPermissionAsync(actorUserId, organizationId, SalesPermissionIds.CustomersManage, cancellationToken))
        {
            return new GetCustomerResult.PermissionDenied();
        }
        string normalized;
        try { normalized = CustomerInput.NormalizeCode(code); }
        catch (InvalidCustomerInputException exception) { return new GetCustomerResult.Invalid(exception.Field, exception.Message); }
        CustomerView? customer = await context.Customers.AsNoTracking()
            .Where(customer => customer.OrganizationId == organizationId.Value && customer.Code == normalized)
            .Select(customer => customer.ToView()).SingleOrDefaultAsync(cancellationToken);
        return customer is null ? new GetCustomerResult.NotFound() : new GetCustomerResult.Found(customer);
    }
}
