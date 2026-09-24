using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Purchasing.Composition;
using ModulithFoundry.Modules.Sales.Composition;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddPostgresDataSource("database");
builder.Services.AddBffAuthentication(builder.Configuration);

builder.Services.AddAccess();
builder.Services.AddInventory();
builder.Services.AddPurchasing();
builder.Services.AddSales();

WebApplication app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapAuthenticationEndpoints();

app.Run();

public partial class Program;
