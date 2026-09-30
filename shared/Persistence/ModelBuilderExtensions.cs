using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Persistence;

public static class ModelBuilderExtensions
{
    public static void ApplyOwnershipFilters<TOwned>(
        this ModelBuilder modelBuilder,
        string filterName,
        Expression<Func<TOwned, bool>> filter)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterName);
        ArgumentNullException.ThrowIfNull(filter);

        foreach (Type entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(metadata => metadata.BaseType is null && !metadata.IsOwned())
                     .Select(metadata => metadata.ClrType)
                     .Where(typeof(TOwned).IsAssignableFrom))
        {
            ParameterExpression entity = Expression.Parameter(entityType, "entity");
            Expression body = new OwnershipExpressionVisitor(filter.Parameters[0], entity)
                .Visit(filter.Body);
            modelBuilder.Entity(entityType).HasQueryFilter(
                filterName,
                Expression.Lambda(body, entity));
        }
    }

    private sealed class OwnershipExpressionVisitor(
        ParameterExpression source,
        ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node) =>
            node.Expression == source
                ? Expression.PropertyOrField(target, node.Member.Name)
                : base.VisitMember(node);

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == source ? target : base.VisitParameter(node);
    }
}
