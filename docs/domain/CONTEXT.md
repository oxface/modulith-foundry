# Wholesale Operations ERP

A multi-tenant product for wholesale organizations to sell stocked goods, reserve inventory, and replenish shortages through purchasing.

## Access

**Organization**:
A business using the product as an independently administered tenant.
_Avoid_: Account, customer tenant

**User**:
A person known to the product who may belong to one or more organizations.
_Avoid_: Identity, account

**External Identity**:
An identity-provider principal linked to a user.
_Avoid_: User, login

**Invitation**:
A single-use, expiring product request for a verified person to join one organization with specified system roles; it does not create or represent an identity-provider account.
_Avoid_: Identity-provider invitation, membership, user

**Membership**:
One tenure in a user's relationship with an organization under product-owned permissions and limits. An active membership grants access, a suspended membership temporarily denies access while retaining its roles, and a removed membership has permanently ended. A removed user may rejoin only by accepting a new invitation, which creates a new membership tenure with its own identity and invited roles.
_Avoid_: Identity-provider role

**Permission**:
A product-owned authorization to perform a business action within an organization.
_Avoid_: Identity-provider role, claim

**Role**:
A named product-defined bundle of permissions assigned to memberships within an organization.
_Avoid_: Identity-provider role, permission

**System Role**:
A role whose identity and permission meaning are defined by the product rather than by an organization administrator.
_Avoid_: Identity-provider role, custom role

**Organization Administrator**:
A membership role allowed to manage the organization and its memberships; every active organization must retain at least one.
_Avoid_: Support user, identity-provider administrator

**Approval Limit**:
A business constraint on the value or scope of actions a membership may approve.
_Avoid_: Role

**Sales Approval Authority**:
The Sales-owned monetary and currency constraints under which a membership may approve a sales order.
_Avoid_: Permission, identity-provider role

## Sales

**Customer**:
A person or organization that buys goods from the selling organization.
_Avoid_: User, tenant, account

**Sales Order**:
A customer's requested purchase of goods that Sales owns from capture through confirmation and completion.
_Avoid_: Purchase, transaction

**Order Fulfilment Process**:
The Sales-owned business process that follows a sales order through inventory reservation, shortage handling, release, cancellation, and completion.
_Avoid_: Saga, orchestrator

## Inventory

**Stock Item**:
A product definition that Inventory recognizes as stockable under a stable SKU and base unit.
_Avoid_: Product catalog entry, sales-order line, supplier item

**Stocking Location**:
An organization-owned place at which Inventory tracks quantities of Stock Items.
_Avoid_: Warehouse zone, bin, organization

**Stock Position**:
The quantity state of one Stock Item at one stocking location.
_Avoid_: Product, inventory record

**Reservation**:
A commitment of available stock to a sales demand without yet recording its physical departure.
_Avoid_: Allocation, stock movement

**Reservation Release**:
The ending of an existing reservation so its quantity becomes available again, normally because the owning sales demand was cancelled or could not continue.
_Avoid_: Stock movement, reservation deletion

**Stock Movement**:
A recorded increase, decrease, or transfer of physical stock.
_Avoid_: Reservation, adjustment

## Purchasing

**Replenishment Requirement**:
A recognized shortage that Purchasing may satisfy through a purchase order.
_Avoid_: Backorder, purchase request

**Purchase Order**:
A commitment issued to a supplier to acquire goods for the organization.
_Avoid_: Sales order, replenishment requirement
