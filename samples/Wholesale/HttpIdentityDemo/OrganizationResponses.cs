using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;
using Rootbolt.ActorIdentity;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;

public sealed record TenantIdentityResponse(ActorKind Kind, string? ActorId, string TenantId);

public sealed record CatalogResponse(TenantIdentityResponse Context, StockAvailability Stock);
