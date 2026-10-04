# E2.3 same-tenant parent/child relationships

Status: implemented for owner review on 2026-10-04 after E2.2 checkpoint `f2dcf2b`.
All new changes remain unstaged for owner review; no commit is authorized.
[The report](../reports/e2-3-tenant-relationships.md) records fresh results and remaining gaps.

## Outcome and ownership

Add Sales-owned `CustomerAddressReference` as an ordinary separately mapped entity, with
GUID ID, Organization key, Customer ID and address line. This is a persistence fixture,
not a rich aggregate or EF owned type. Both parent and child explicitly register the
existing ownership utility. No cross-module EF relationship is added.

Keep the customer's GUID primary key and add a native alternate key
`(OrganizationKey, Id)`. The required address foreign key `(OrganizationKey, CustomerId)`
references it. Thus an Alpha-owned child referencing a Beta customer's ID cannot satisfy
the database constraint, even without loading that customer. Use native `DeleteBehavior.Restrict`:
the consumer explicitly removes addresses before deleting a customer. Mapping, key choice,
deletion policy and address population remain consumer-owned; no relationship facade is planned.

An incremental Sales migration adds the key and address table, preserving E2.2 customer rows.
Inventory's model/history are untouched. The finite sample explicitly saves and reads an
address per fixture customer, using tenant-scoped ordinary LINQ. First/repeat execution
remains demonstrated. Initial migrations remain frozen; the sample schema policy grows
only to cover the actual new key/foreign-key operations.

## Focused proofs

- Positive detached-ID child insertion/read and same-tenant reparenting through Sales.
- Alpha-owned child plus a Beta parent ID fails on PostgreSQL for both insert and update;
  the parent is not loaded, the child owner matches the current tenant, and fresh scopes
  confirm no insertion or persisted reparenting. Inspect the actual foreign-key failure.
- Child reads remain tenant-filtered and foreign ownership is rejected by explicit save
  validation. Avoid repeating E2.1's complete overload/helper matrix.
- Restricted parent deletion fails with a remaining child; deliberate child removal then
  parent deletion succeeds. This protects the selected consumer lifecycle policy.
- Apply the initial Sales migration, insert an existing customer, then apply the new
  migration and use the relationship while retaining customer/history data.
- Fast policies classify both entities, inspect their actual tenant-bearing relationship
  and module-local migration operations, and detect stale snapshots. No synthetic tests
  of xUnit/EF detection or generic migration parsing are added.

## Compatibility and limits

Alternate-key components are immutable in native EF. Changing a customer's Organization
may therefore fail during native change detection before the library returns its typed
ownership failure. Do not add exception translation or relax the relationship to force
one failure shape. Supported detached same-owner customer updates must continue to work.

No new library mechanism is expected. If evidence exposes an actual utility defect,
document it before revising its reviewed interface. Versions, operation-level transaction
faults, shared cross-module transactions, HTTP ingress and membership remain later slices.
Database constraints preserve relationships; they do not establish authorization or prevent
privileged ownership transfers. PostgreSQL remains the only exercised provider.

Native references: [principal/foreign keys](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/foreign-and-principal-keys)
and [alternate keys](https://learn.microsoft.com/en-us/ef/core/modeling/keys).
