using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Api.Modules.Access;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Purchasing.Composition;
using ModulithFoundry.Modules.Sales.Composition;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddPostgresDataSource("database");
builder.Services.AddBffAuthentication(builder.Configuration);
builder.Services.AddAccessApi();

builder.Services.AddAccessModule();
builder.Services.AddInventoryModule();
builder.Services.AddPurchasingModule();
builder.Services.AddSalesModule();

WebApplication app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseOrganizationScope();
app.MapDefaultEndpoints();
app.MapAuthenticationApi();
app.MapAccessApi();

app.Run();

public partial class Program;
