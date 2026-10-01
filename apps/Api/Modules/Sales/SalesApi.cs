using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Api.Modules.Sales.ApprovalAuthorities;
using ModulithFoundry.Api.Modules.Sales.Customers;
using ModulithFoundry.Api.Modules.Sales.Orders;

namespace ModulithFoundry.Api.Modules.Sales;

internal static class SalesApi
{
    internal static IEndpointRouteBuilder MapSalesApi(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder sales = endpoints
            .MapGroup("/api/o/{organizationSlug}/sales")
            .RequireAuthorization()
            .WithMetadata(OrganizationScopeMetadata.Instance);
        sales.MapCustomerEndpoints();
        sales.MapSalesOrderEndpoints();
        sales.MapSalesApprovalAuthorityEndpoints();
        return endpoints;
    }
}
