# Applications

`apps` contains executable entry points. Business capabilities remain first-class siblings under [`modules`](../modules); directory placement does not reverse the dependency rule that applications compose modules and modules never depend on applications.

- `AppHost` is the conventional C# Aspire orchestration project. Resource wiring begins in Increment 1.2.
- `Api` is the composition root and eventual HTTP entry point; it owns no business workflow.
- `Migrator` will become the finite migration executable in Increment 1.2.

The deferred Vite frontend will live at `apps/Web`; its module-specific screens remain feature folders inside that application until a real independent frontend package boundary is justified. [`shared/ServiceDefaults`](../shared/ServiceDefaults) will hold shared health and OpenTelemetry setup in Increment 1.2.
