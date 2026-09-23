---
status: accepted
---

# Keep product authorization relational and module-owned

V1 stores organization memberships and role assignments in Access while defining system roles and their coarse permission bundles as reviewed product code with stable textual identifiers. Organization Administrator governs access administration rather than granting universal business authority. Each business module defines stable permission tokens and remains authoritative for resource state, approval limits, separation of duties, ownership, and aggregate invariants. Application handlers enforce authorization for every entry path; ASP.NET Core policies are boundary adapters rather than the sole enforcement mechanism. OpenFGA, tenant-defined roles, direct grants, explicit denies, role inheritance, per-object ACLs, field permissions, support impersonation, and distributed authorization caching create no v1 implementation. OpenFGA is reconsidered only for a demonstrated relationship graph such as nested groups, direct object sharing, deep inheritance, delegated partner access, or reverse reachability queries.
