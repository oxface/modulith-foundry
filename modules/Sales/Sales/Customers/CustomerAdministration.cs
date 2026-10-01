using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Customers.CreateCustomer;
using ModulithFoundry.Modules.Sales.Customers.Queries;

namespace ModulithFoundry.Modules.Sales.Customers;

internal sealed class CustomerAdministration(CreateCustomerHandler create, CustomerQueries queries) : ICustomerAdministration
{
    public Task<CreateCustomerResult> CreateAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default) =>
        create.HandleAsync(command, cancellationToken);

    public Task<GetCustomerResult> GetByCodeAsync(UserId actorUserId, OrganizationId organizationId, string code, CancellationToken cancellationToken = default) =>
        queries.GetByCodeAsync(actorUserId, organizationId, code, cancellationToken);
}
