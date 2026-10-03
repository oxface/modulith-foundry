namespace ModulithFoundry.Modules.Sales.ApprovalAuthorities;

internal sealed class InvalidApprovalLimitException(string field, string detail) : Exception(detail)
{
    internal string Field { get; } = field;
}
