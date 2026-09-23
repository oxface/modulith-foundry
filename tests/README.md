# Tests

Increment 1.1 introduces only `ArchitectureTests`, which runs in the container-free Fast lane. It checks the declared solution/project graph as well as compiled dependencies with ArchUnitNET.

Later increments add focused domain, application, PostgreSQL, topology, broker-failure, browser, and Azure compatibility projects only when their real behavior exists. The canonical Fast-lane command is:

```bash
dotnet test --solution ModulithFoundry.slnx
```

The repository uses xUnit v3 on Microsoft Testing Platform v2. MTP is the test execution platform; ArchUnitNET supplies architecture assertions and does not require MTP itself.

Test names use `{OperationOrRule}_{Scenario}_{ExpectedOutcome}`. Invariants with no meaningful scenario use a concise two-part fact, such as `ProjectDependencyGraph_IsAcyclic`.
