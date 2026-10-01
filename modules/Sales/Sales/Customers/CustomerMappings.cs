using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Modules.Sales.Customers;

internal static class CustomerMappings
{
    internal static CustomerView ToView(this Customer customer) =>
        new(new CustomerId(customer.Id), customer.Code, customer.Name);
}
