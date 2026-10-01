using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public static class InventoryAuthorizationManifest
{
    public static SystemRoleManifest Instance { get; } =
        new(
            "inventory",
            [
                new(InventoryPermissionIds.ItemsManage, "Manage stock items"),
                new(InventoryPermissionIds.LocationsManage, "Manage stocking locations"),
                new(InventoryPermissionIds.StockAdjust, "Adjust stock"),
                new(InventoryPermissionIds.StockView, "View stock"),
                new(InventoryPermissionIds.ProjectionRebuild, "Rebuild stock projections"),
            ],
            [
                new(
                    InventoryRoleIds.Manager,
                    "Inventory Manager",
                    [
                        InventoryPermissionIds.ItemsManage,
                        InventoryPermissionIds.LocationsManage,
                        InventoryPermissionIds.StockAdjust,
                        InventoryPermissionIds.StockView,
                        InventoryPermissionIds.ProjectionRebuild,
                    ]
                ),
            ]
        );
}
