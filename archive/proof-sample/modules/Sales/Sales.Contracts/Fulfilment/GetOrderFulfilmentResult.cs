namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record GetOrderFulfilmentResult
{
    private GetOrderFulfilmentResult() { }

    public sealed record Found(OrderFulfilmentView Process) : GetOrderFulfilmentResult;

    public sealed record NotFound : GetOrderFulfilmentResult;

    public sealed record PermissionDenied : GetOrderFulfilmentResult;
}
