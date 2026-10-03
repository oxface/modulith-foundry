namespace ModulithFoundry.ExecutionIdentity;

/// <summary>A declared context requirement failed; consumers own failure presentation.</summary>
public sealed class ContextRequirementException : InvalidOperationException
{
    internal ContextRequirementException(ContextRequirement requirement)
        : base(
            requirement switch
            {
                ContextRequirement.TenantRequired => "The operation requires a selected tenant.",
                ContextRequirement.IdentifiedActorRequired =>
                    "The operation requires an identified actor.",
                _ => throw new ArgumentOutOfRangeException(nameof(requirement)),
            }
        )
    {
        Requirement = requirement;
    }

    public ContextRequirement Requirement { get; }
}
