using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Contracts;

public static class PurchasingAuthorizationManifest
{
    public static SystemRoleManifest Instance { get; } =
        new(
            "purchasing",
            [
                new(PurchasingPermissionIds.RequirementsView, "View replenishment requirements"),
                new(PurchasingPermissionIds.PurchaseOrdersManage, "Manage purchase orders"),
            ],
            [
                new(
                    PurchasingRoleIds.Agent,
                    "Purchasing Agent",
                    [
                        PurchasingPermissionIds.RequirementsView,
                        PurchasingPermissionIds.PurchaseOrdersManage,
                    ]
                ),
            ]
        );
}
