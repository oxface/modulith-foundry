# Shared infrastructure instructions

Follow the repository-wide instructions in [../AGENTS.md](../AGENTS.md).

- Keep this subtree technical; business concepts and module contracts remain under `modules`.
- Add a shared abstraction only after two concrete consumers or adapters demonstrate the same need.
- Keep `ServiceDefaults` limited to process-wide health, telemetry, discovery, and resilient HTTP defaults.
