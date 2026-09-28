namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationCreation
{
    Task<CreateOrganizationResult> CreateOrganizationAsync(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken = default);
}
