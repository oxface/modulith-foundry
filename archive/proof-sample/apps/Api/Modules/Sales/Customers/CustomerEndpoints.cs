using System.Diagnostics;
using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Api.Modules.Sales.Customers;

internal static class CustomerEndpoints
{
    internal static RouteGroupBuilder MapCustomerEndpoints(this RouteGroupBuilder sales)
    {
        RouteGroupBuilder customers = sales.MapGroup("/customers");
        customers.MapPost("", CreateCustomerAsync).RequireBffAntiforgery();
        customers.MapGet("/{code}", GetCustomerAsync);
        return sales;
    }

    private static async Task<IResult> CreateCustomerAsync(
        CreateCustomerRequest request,
        IOrganizationContextAccessor contextAccessor,
        ICustomerAdministration administration,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        CreateCustomerResult result = await administration.CreateAsync(
            new(context.UserId, context.OrganizationId, request.Code, request.Name),
            cancellationToken
        );
        return result switch
        {
            CreateCustomerResult.Created created => TypedResults.Created(
                $"/api/o/{Uri.EscapeDataString(context.OrganizationSlug)}/sales/customers/{Uri.EscapeDataString(created.Customer.Code)}",
                ToResponse(created.Customer)
            ),
            CreateCustomerResult.Invalid invalid => InvalidCustomer(invalid.Field, invalid.Detail),
            CreateCustomerResult.CodeUnavailable => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Customer code unavailable",
                detail: "This organization already has a customer with that code."
            ),
            CreateCustomerResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<IResult> GetCustomerAsync(
        string code,
        IOrganizationContextAccessor contextAccessor,
        ICustomerAdministration administration,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        GetCustomerResult result = await administration.GetByCodeAsync(
            context.UserId,
            context.OrganizationId,
            code,
            cancellationToken
        );
        return result switch
        {
            GetCustomerResult.Found found => TypedResults.Ok(ToResponse(found.Customer)),
            GetCustomerResult.NotFound => Results.NotFound(),
            GetCustomerResult.Invalid invalid => InvalidCustomer(invalid.Field, invalid.Detail),
            GetCustomerResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static IResult InvalidCustomer(string field, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid customer",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["field"] = field }
        );

    private static CustomerResponse ToResponse(CustomerView customer) =>
        new(customer.CustomerId.Value, customer.Code, customer.Name);

    private sealed record CreateCustomerRequest(string Code, string Name);

    private sealed record CustomerResponse(Guid CustomerId, string Code, string Name);
}
