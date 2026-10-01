namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record GetCustomerResult
{
    private GetCustomerResult() { }
    public sealed record Found(CustomerView Customer) : GetCustomerResult;
    public sealed record Invalid(string Field, string Detail) : GetCustomerResult;
    public sealed record NotFound : GetCustomerResult;
    public sealed record PermissionDenied : GetCustomerResult;
}
