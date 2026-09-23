---
status: accepted
---

# Give each module an owned PostgreSQL schema

The application will use one PostgreSQL database with a separate schema, DbContext, migration history, and persistence implementation per module. Modules may carry opaque identifiers from other modules but may not query, update, map, join, or create foreign keys to another module's tables; this trades database-level cross-module convenience for ownership that can be tested now and extracted later if a real service boundary emerges.
