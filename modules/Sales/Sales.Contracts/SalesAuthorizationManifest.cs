using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public static class SalesAuthorizationManifest
{
    public static SystemRoleManifest Instance { get; } = new(
        "sales",
        [
            new(SalesPermissionIds.CustomersManage, "Manage customers"),
            new(SalesPermissionIds.OrdersCreate, "Create sales orders"),
            new(SalesPermissionIds.OrdersSubmit, "Submit sales orders"),
            new(SalesPermissionIds.ApprovalAuthoritiesManage, "Manage sales approval authorities"),
            new(SalesPermissionIds.OrdersApprove, "Approve sales orders"),
            new(SalesPermissionIds.OrdersCancel, "Cancel sales orders"),
            new(SalesPermissionIds.OrdersView, "View sales orders"),
        ],
        [
            new(
                SalesRoleIds.Clerk,
                "Sales Clerk",
                [
                    SalesPermissionIds.CustomersManage,
                    SalesPermissionIds.OrdersCreate,
                    SalesPermissionIds.OrdersSubmit,
                    SalesPermissionIds.OrdersCancel,
                    SalesPermissionIds.OrdersView,
                ]),
            new(
                SalesRoleIds.Manager,
                "Sales Manager",
                [
                    SalesPermissionIds.CustomersManage,
                    SalesPermissionIds.OrdersCreate,
                    SalesPermissionIds.OrdersSubmit,
                    SalesPermissionIds.ApprovalAuthoritiesManage,
                    SalesPermissionIds.OrdersCancel,
                    SalesPermissionIds.OrdersView,
                ]),
            new(
                SalesRoleIds.Approver,
                "Sales Approver",
                [SalesPermissionIds.OrdersApprove, SalesPermissionIds.OrdersView]),
        ]);
}
