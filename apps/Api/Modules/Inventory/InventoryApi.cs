using System.Diagnostics;
using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Api.Modules.Inventory;

internal static class InventoryApi
{
    internal static IEndpointRouteBuilder MapInventoryApi(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder inventory = endpoints.MapGroup(
                "/api/o/{organizationSlug}/inventory")
            .RequireAuthorization()
            .WithMetadata(OrganizationScopeMetadata.Instance);

        RouteGroupBuilder items = inventory.MapGroup("/items");
        items.MapGet("", ListStockItemsAsync);
        items.MapPost("", CreateStockItemAsync).RequireBffAntiforgery();
        items.MapPut("/{sku}/description", ChangeStockItemDescriptionAsync)
            .RequireBffAntiforgery();
        items.MapPost("/{sku}/activate", ActivateStockItemAsync).RequireBffAntiforgery();
        items.MapPost("/{sku}/deactivate", DeactivateStockItemAsync).RequireBffAntiforgery();

        RouteGroupBuilder locations = inventory.MapGroup("/locations");
        locations.MapGet("", ListStockingLocationsAsync);
        locations.MapPost("", CreateStockingLocationAsync).RequireBffAntiforgery();
        locations.MapPut("/{code}/name", RenameStockingLocationAsync)
            .RequireBffAntiforgery();
        locations.MapPost("/{code}/activate", ActivateStockingLocationAsync)
            .RequireBffAntiforgery();
        locations.MapPost("/{code}/deactivate", DeactivateStockingLocationAsync)
            .RequireBffAntiforgery();

        inventory.MapStockPositionEndpoints();

        return endpoints;
    }

    private static async Task<IResult> CreateStockItemAsync(
        CreateStockItemRequest request,
        IOrganizationContextAccessor contextAccessor,
        IStockItemAdministration administration,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        CreateStockItemResult result = await administration.CreateAsync(
            new CreateStockItemCommand(
                context.UserId,
                context.OrganizationId,
                request.Sku,
                request.Description,
                request.BaseUnitCode),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ChangeStockItemDescriptionAsync(
        string sku,
        ChangeStockItemDescriptionRequest request,
        IOrganizationContextAccessor contextAccessor,
        IStockItemAdministration administration,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        ChangeStockItemDescriptionResult result = await administration.ChangeDescriptionAsync(
            new ChangeStockItemDescriptionCommand(
                context.UserId,
                context.OrganizationId,
                sku,
                request.Description),
            cancellationToken);
        return ToResult(result);
    }

    private static Task<IResult> ActivateStockItemAsync(
        string sku,
        IOrganizationContextAccessor contextAccessor,
        IStockItemAdministration administration,
        CancellationToken cancellationToken) =>
        SetStockItemActiveAsync(sku, true, contextAccessor, administration, cancellationToken);

    private static Task<IResult> DeactivateStockItemAsync(
        string sku,
        IOrganizationContextAccessor contextAccessor,
        IStockItemAdministration administration,
        CancellationToken cancellationToken) =>
        SetStockItemActiveAsync(sku, false, contextAccessor, administration, cancellationToken);

    private static async Task<IResult> SetStockItemActiveAsync(
        string sku,
        bool isActive,
        IOrganizationContextAccessor contextAccessor,
        IStockItemAdministration administration,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        SetStockItemActiveResult result = await administration.SetActiveAsync(
            new SetStockItemActiveCommand(
                context.UserId,
                context.OrganizationId,
                sku,
                isActive),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ListStockItemsAsync(
        IOrganizationContextAccessor contextAccessor,
        IStockItemAdministration administration,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        ListStockItemsResult result = await administration.ListAsync(
            context.UserId,
            context.OrganizationId,
            cancellationToken);
        return result switch
        {
            ListStockItemsResult.Listed listed => TypedResults.Ok(
                (IReadOnlyList<StockItemResponse>)[.. listed.Items.Select(ToResponse)]),
            ListStockItemsResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<IResult> CreateStockingLocationAsync(
        CreateStockingLocationRequest request,
        IOrganizationContextAccessor contextAccessor,
        IStockingLocationAdministration administration,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        CreateStockingLocationResult result = await administration.CreateAsync(
            new CreateStockingLocationCommand(
                context.UserId,
                context.OrganizationId,
                request.Code,
                request.Name),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> RenameStockingLocationAsync(
        string code,
        RenameStockingLocationRequest request,
        IOrganizationContextAccessor contextAccessor,
        IStockingLocationAdministration administration,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        RenameStockingLocationResult result = await administration.RenameAsync(
            new RenameStockingLocationCommand(
                context.UserId,
                context.OrganizationId,
                code,
                request.Name),
            cancellationToken);
        return ToResult(result);
    }

    private static Task<IResult> ActivateStockingLocationAsync(
        string code,
        IOrganizationContextAccessor contextAccessor,
        IStockingLocationAdministration administration,
        CancellationToken cancellationToken) =>
        SetStockingLocationActiveAsync(code, true, contextAccessor, administration, cancellationToken);

    private static Task<IResult> DeactivateStockingLocationAsync(
        string code,
        IOrganizationContextAccessor contextAccessor,
        IStockingLocationAdministration administration,
        CancellationToken cancellationToken) =>
        SetStockingLocationActiveAsync(code, false, contextAccessor, administration, cancellationToken);

    private static async Task<IResult> SetStockingLocationActiveAsync(
        string code,
        bool isActive,
        IOrganizationContextAccessor contextAccessor,
        IStockingLocationAdministration administration,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        SetStockingLocationActiveResult result = await administration.SetActiveAsync(
            new SetStockingLocationActiveCommand(
                context.UserId,
                context.OrganizationId,
                code,
                isActive),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ListStockingLocationsAsync(
        IOrganizationContextAccessor contextAccessor,
        IStockingLocationAdministration administration,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        ListStockingLocationsResult result = await administration.ListAsync(
            context.UserId,
            context.OrganizationId,
            cancellationToken);
        return result switch
        {
            ListStockingLocationsResult.Listed listed => TypedResults.Ok(
                (IReadOnlyList<StockingLocationResponse>)[.. listed.Locations.Select(ToResponse)]),
            ListStockingLocationsResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static IResult ToResult(CreateStockItemResult result) =>
        result switch
        {
            CreateStockItemResult.Created created =>
                Results.Json(ToResponse(created.Item), statusCode: StatusCodes.Status201Created),
            CreateStockItemResult.Invalid invalid => InvalidReferenceData(invalid.Field, invalid.Detail),
            CreateStockItemResult.SkuUnavailable unavailable => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Stock item SKU unavailable",
                detail: $"Stock item SKU '{unavailable.Sku}' is unavailable."),
            CreateStockItemResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };

    private static IResult ToResult(ChangeStockItemDescriptionResult result) =>
        result switch
        {
            ChangeStockItemDescriptionResult.Changed changed => TypedResults.Ok(ToResponse(changed.Item)),
            ChangeStockItemDescriptionResult.Unchanged unchanged =>
                TypedResults.Ok(ToResponse(unchanged.Item)),
            ChangeStockItemDescriptionResult.Invalid invalid =>
                InvalidReferenceData(invalid.Field, invalid.Detail),
            ChangeStockItemDescriptionResult.NotFound => Results.NotFound(),
            ChangeStockItemDescriptionResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };

    private static IResult ToResult(SetStockItemActiveResult result) =>
        result switch
        {
            SetStockItemActiveResult.Changed changed => TypedResults.Ok(ToResponse(changed.Item)),
            SetStockItemActiveResult.Unchanged unchanged => TypedResults.Ok(ToResponse(unchanged.Item)),
            SetStockItemActiveResult.Invalid invalid =>
                InvalidReferenceData(invalid.Field, invalid.Detail),
            SetStockItemActiveResult.NotFound => Results.NotFound(),
            SetStockItemActiveResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };

    private static IResult ToResult(CreateStockingLocationResult result) =>
        result switch
        {
            CreateStockingLocationResult.Created created => Results.Json(
                ToResponse(created.Location),
                statusCode: StatusCodes.Status201Created),
            CreateStockingLocationResult.Invalid invalid =>
                InvalidReferenceData(invalid.Field, invalid.Detail),
            CreateStockingLocationResult.CodeUnavailable unavailable => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Stocking location code unavailable",
                detail: $"Stocking location code '{unavailable.Code}' is unavailable."),
            CreateStockingLocationResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };

    private static IResult ToResult(RenameStockingLocationResult result) =>
        result switch
        {
            RenameStockingLocationResult.Renamed renamed => TypedResults.Ok(ToResponse(renamed.Location)),
            RenameStockingLocationResult.Unchanged unchanged =>
                TypedResults.Ok(ToResponse(unchanged.Location)),
            RenameStockingLocationResult.Invalid invalid =>
                InvalidReferenceData(invalid.Field, invalid.Detail),
            RenameStockingLocationResult.NotFound => Results.NotFound(),
            RenameStockingLocationResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };

    private static IResult ToResult(SetStockingLocationActiveResult result) =>
        result switch
        {
            SetStockingLocationActiveResult.Changed changed =>
                TypedResults.Ok(ToResponse(changed.Location)),
            SetStockingLocationActiveResult.Unchanged unchanged =>
                TypedResults.Ok(ToResponse(unchanged.Location)),
            SetStockingLocationActiveResult.Invalid invalid =>
                InvalidReferenceData(invalid.Field, invalid.Detail),
            SetStockingLocationActiveResult.NotFound => Results.NotFound(),
            SetStockingLocationActiveResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };

    private static IResult InvalidReferenceData(string field, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid inventory reference data",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["field"] = field });

    private static StockItemResponse ToResponse(StockItemView item) =>
        new(
            item.StockItemId.Value,
            item.Sku,
            item.Description,
            item.BaseUnitCode,
            item.IsActive);

    private static StockingLocationResponse ToResponse(StockingLocationView location) =>
        new(
            location.StockingLocationId.Value,
            location.Code,
            location.Name,
            location.IsActive);

    private sealed record CreateStockItemRequest(
        string Sku,
        string Description,
        string BaseUnitCode);

    private sealed record ChangeStockItemDescriptionRequest(string Description);

    private sealed record CreateStockingLocationRequest(string Code, string Name);

    private sealed record RenameStockingLocationRequest(string Name);

    private sealed record StockItemResponse(
        Guid StockItemId,
        string Sku,
        string Description,
        string BaseUnitCode,
        bool IsActive);

    private sealed record StockingLocationResponse(
        Guid StockingLocationId,
        string Code,
        string Name,
        bool IsActive);
}
