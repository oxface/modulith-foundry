using Rootbolt.ActorIdentity;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;

public sealed record IdentityResponse(ActorKind Kind, string? ActorId);
