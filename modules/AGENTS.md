# Module instructions

Follow the repository-wide instructions in [../AGENTS.md](../AGENTS.md).

- Preserve the project dependency rules in [../docs/modules/README.md](../docs/modules/README.md); the architecture tests are executable policy, not the only source of intent.
- Read the owning module charter before changing a module's responsibilities or Contracts surface.
- Keep business behavior in its owning module. The API composes modules and process-wide concerns only.
- Do not create shared abstractions, generic repositories, mediators, or cross-module infrastructure for hypothetical use.
- Keep implementation types internal unless the type is a deliberate module composition entry point. Contracts must not expose web, persistence, transport, entity, `DbContext`, or `IQueryable` types.
