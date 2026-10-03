using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public interface ICustomerAdministration
{
    Task<CreateCustomerResult> CreateAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default
    );
    Task<GetCustomerResult> GetByCodeAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        string code,
        CancellationToken cancellationToken = default
    );
}
