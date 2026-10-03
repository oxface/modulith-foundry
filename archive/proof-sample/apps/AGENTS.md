# Application instructions

Follow the repository-wide instructions in [../AGENTS.md](../AGENTS.md).

- Keep business workflows in their owning module; `Api` is the HTTP entry point and composition root only.
- Keep `AppHost` limited to local orchestration and topology. Use Aspire lifecycle commands rather than running the AppHost with `dotnet run`.
- Keep `Migrator` finite: it applies the declared module migrations and exits with a meaningful status.
- Add application references only when the executable actually composes or orchestrates the referenced project.
