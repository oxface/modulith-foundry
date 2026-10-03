---
status: accepted
---

# Use a single-process modular monolith

The reference product will be one application process and one application deployment containing at least three business modules. Modules collaborate through small in-process interfaces rather than HTTP, which preserves explicit seams and a future extraction path without paying distributed-systems costs before a module has an independent scaling, release, security, or ownership reason.

