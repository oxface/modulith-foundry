using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Api.Infrastructure;
using ModulithFoundry.Api.Modules.Access;
using ModulithFoundry.Api.Modules.Inventory;
using ModulithFoundry.Api.Modules.Purchasing;
using ModulithFoundry.Api.Modules.Sales;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Purchasing.Composition;
using ModulithFoundry.Modules.Sales.Composition;
using OpenTelemetry.Metrics;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.ConfigureOpenTelemetryMeterProvider(metrics =>
    metrics.AddMeter(
        InventoryMessaging.MeterName,
        SalesMessaging.MeterName,
        PurchasingMessaging.MeterName
    )
);
builder.AddPostgresDataSource("database");
builder.Services.AddApiErrorHandling();
builder.Services.AddApiRequestValidation();
builder.Services.AddBffAuthentication(builder.Configuration);
builder.Services.AddAccessApi();

builder.Services.AddAccessModule(builder.Configuration);
builder.Services.AddInventoryModule();
builder.Services.AddPurchasingModule();
builder.Services.AddSalesModule();

// Establish the durable outcome subscription before Inventory can publish recovered outbox work.
builder.Host.AddSalesMessaging(builder.Configuration);
builder.Host.AddPurchasingMessaging(builder.Configuration);
builder.Host.AddInventoryMessaging(builder.Configuration);

WebApplication app = builder.Build();
app.UseApiErrorHandling();
app.UseAuthentication();
app.UseAuthorization();
app.UseOrganizationScope();
app.MapDefaultEndpoints();
app.MapAuthenticationApi();
app.MapAccessApi();
app.MapInventoryApi();
app.MapSalesApi();
app.MapPurchasingApi();

app.Run();

public partial class Program;
