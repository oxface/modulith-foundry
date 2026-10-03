namespace ModulithFoundry.Modules.Purchasing.Contracts;

/// <summary>Trusted initialization; requires the Purchasing subscription to be bound first.</summary>
public interface IStockItemProjectionBootstrapper
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}
