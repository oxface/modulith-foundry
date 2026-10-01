# Module map

The reference product is a wholesale-operations ERP with four business modules. These charters describe business ownership and the interface each module presents; they do not prescribe internal folders or create a reusable framework.

| Module | Owns | Does not own |
| --- | --- | --- |
| [Access](access.md) | Organizations, users, external identities, memberships, invitations, role assignments | Identity-provider accounts, Sales/Inventory/Purchasing policy |
| [Sales](sales.md) | Customers, sales orders, approval policy, Order Fulfilment Process | Stock truth, reservations, replenishment requirements |
| [Inventory](inventory.md) | Stock Items, Stocking Locations, Stock Positions, reservations, stock movements | Sales demand, suppliers, purchase orders |
| [Purchasing](purchasing.md) | Replenishment Requirements, supplier sourcing, eventual purchase orders | Stock truth, sales-order state |

## Dependency map

Draft Sales Orders exercise the Sales implementation → Inventory.Contracts query. Sales.Contracts also references Inventory.Contracts solely for the Inventory-owned `StockItemId` in its line inputs/results; snapshot DTOs remain Sales-owned. This exceptional Contracts edge is allowlisted and participates in cycle checks.

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

An implementation project may reference another module's Contracts project when an implemented use case requires it. A Contracts project may reference another Contracts project only when its own interface genuinely contains an owner-defined stable identifier, value type, or composition manifest, such as `OrganizationId`, `StockItemId`, or the Access-owned system-role definition used by each business module's authorization manifest; architecture tests keep those edges in an explicit allowlist and reject cycles and transitive DTO graphs. Contracts never reference module implementations. The API references implementations for composition but contains no business workflow.

The receiving module owns a durable integration-command type in its Contracts project. The module that publishes a fact owns the integration-event type in its Contracts project. Contracts contain no ASP.NET Core, EF Core, Rebus, identity-provider, or persistence types. Immediate commands and queries use capability-oriented in-process interfaces; durable commands and events use the broker. They are not interchangeable solely to make later extraction appear automatic.

HTTP endpoints and request/response models are host-owned ingress adapters under `apps/Api/Modules/{Module}`. They invoke module Contracts and contain no business workflow. Module implementations do not reference ASP.NET Core, and workflow-only operations remain reachable only through trusted composition or message adapters. The API groups each module's routes behind `Map{Module}Api`; this does not make HTTP the interface between modules.

Internal application-operation and query naming follows [the application-code conventions](../conventions/application-code.md).
