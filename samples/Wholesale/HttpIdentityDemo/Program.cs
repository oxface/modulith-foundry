using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;

var builder = WebApplication.CreateBuilder(args);
DemoComposition.AddIdentityServices(builder.Services, builder.Configuration);
var app = builder.Build();
DemoComposition.ConfigureHttp(app);
await app.RunAsync();
