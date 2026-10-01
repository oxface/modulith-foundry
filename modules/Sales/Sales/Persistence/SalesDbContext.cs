using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.ApprovalAuthorities;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Customers;
using ModulithFoundry.Modules.Sales.Fulfilment;
using ModulithFoundry.Modules.Sales.Messaging.Persistence;
using ModulithFoundry.Modules.Sales.Orders;
using ModulithFoundry.Modules.Sales.Orders.Activity;
using ModulithFoundry.Modules.Sales.Orders.Persistence;
using ModulithFoundry.Persistence;

namespace ModulithFoundry.Modules.Sales.Persistence;

internal sealed class SalesDbContext(
    DbContextOptions<SalesDbContext> options,
    IOrganizationContextAccessor organizationContextAccessor
) : DbContext(options)
{
    internal const string Schema = "sales";
    internal const string OrganizationScopeFilter = "OrganizationScope";

    internal DbSet<Customer> Customers => Set<Customer>();

    internal DbSet<SalesApprovalAuthority> ApprovalAuthorities => Set<SalesApprovalAuthority>();

    internal DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    internal DbSet<OrderFulfilmentProcess> FulfilmentProcesses => Set<OrderFulfilmentProcess>();

    internal DbSet<SalesOrderActivity> OrderActivity => Set<SalesOrderActivity>();

    internal DbSet<SalesOrderNumber> SalesOrderNumbers => Set<SalesOrderNumber>();

    internal DbSet<SalesAuditEntry> AuditEntries => Set<SalesAuditEntry>();

    internal DbSet<SalesInboxReceipt> InboxReceipts => Set<SalesInboxReceipt>();

    internal DbSet<SalesOutboxMessage> OutboxMessages => Set<SalesOutboxMessage>();

    private Guid? workflowOrganizationId;

    internal void UseWorkflowOrganization(Guid organizationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        if (
            organizationContextAccessor.OrganizationContext is not null
            || workflowOrganizationId.HasValue
        )
            throw new InvalidOperationException(
                "Workflow scope requires a fresh non-human Sales context."
            );
        workflowOrganizationId = organizationId;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
        modelBuilder.ApplyOwnershipFilters<IOrganizationOwned>(
            OrganizationScopeFilter,
            entity =>
                CurrentOrganizationId.HasValue && entity.OrganizationId == CurrentOrganizationId
        );
    }

    private Guid? CurrentOrganizationId =>
        organizationContextAccessor.OrganizationContext?.OrganizationId.Value
        ?? workflowOrganizationId;
}
