using ModulithFoundry.ActorIdentity;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Inventory.Contracts;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;

public sealed record TenantIdentityResponse(ActorKind Kind, string? ActorId, string TenantId);

public sealed record CatalogResponse(TenantIdentityResponse Context, StockAvailability Stock);
