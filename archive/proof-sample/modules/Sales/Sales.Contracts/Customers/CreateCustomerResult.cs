namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record CreateCustomerResult
{
    private CreateCustomerResult() { }

    public sealed record Created(CustomerView Customer) : CreateCustomerResult;

    public sealed record Invalid(string Field, string Detail) : CreateCustomerResult;

    public sealed record CodeUnavailable(string Code) : CreateCustomerResult;

    public sealed record PermissionDenied : CreateCustomerResult;
}
