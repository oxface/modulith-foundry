# Module map

The reference product is a wholesale-operations ERP with four business modules. These charters describe business ownership and the interface each module presents; they do not prescribe internal folders or create a reusable framework.

| Module | Owns | Does not own |
| --- | --- | --- |
| [Access](access.md) | Organizations, users, external identities, memberships, invitations, role assignments | Identity-provider accounts, Sales/Inventory/Purchasing policy |
| [Sales](sales.md) | Customers, sales orders, approval policy, Order Fulfilment Process | Stock truth, reservations, replenishment requirements |
| [Inventory](inventory.md) | Stock Items, Stocking Locations, Stock Positions, reservations, stock movements | Sales demand, suppliers, purchase orders |
| [Purchasing](purchasing.md) | Replenishment Requirements, supplier sourcing, eventual purchase orders | Stock truth, sales-order state |

## Dependency map

These cross-module arrows describe accepted use-case direction. Add each Contracts reference only when the implementing slice actually uses it; the initial skeleton does not carry speculative references.

```text
Api
  -> Access
  -> Sales
  -> Inventory
  -> Purchasing

Sales implementation
  -> Access.Contracts
  -> Inventory.Contracts
  -> Purchasing.Contracts

Inventory implementation
  -> Access.Contracts

Purchasing implementation
  -> Access.Contracts
  -> Inventory.Contracts
```

An implementation project may reference another module's Contracts project when an implemented use case requires it. A Contracts project may reference another Contracts project only when its own interface genuinely contains an owner-defined stable identifier or value type, such as `OrganizationId` or `StockItemId`; architecture tests reject cycles and transitive DTO graphs. Contracts never reference module implementations. The API references implementations for composition but contains no business workflow.

The receiving module owns a durable integration-command type in its Contracts project. The module that publishes a fact owns the integration-event type in its Contracts project. Contracts contain no ASP.NET Core, EF Core, Rebus, identity-provider, or persistence types. Immediate commands and queries use capability-oriented in-process interfaces; durable commands and events use the broker. They are not interchangeable solely to make later extraction appear automatic.

HTTP endpoints and request models live with their vertical slice inside the implementation project. A type being public to the CLR does not make it an HTTP operation. Workflow-only operations are reachable only through trusted composition or message adapters.
