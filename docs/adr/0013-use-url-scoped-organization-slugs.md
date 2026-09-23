---
status: accepted
---

# Use URL-scoped organization slugs

Organization-scoped browser and BFF routes use a customer-proposed, normalized, globally unique slug such as `/o/acme-industrial/...`. The route enables bookmarks and independent tabs but only selects an organization; current membership and permission are checked before constructing the request tenant context. The BFF session stores neither an active nor last-used organization. The root route redirects when the user currently has one organization and otherwise shows a chooser. Organization slugs are immutable in normal workflows; an exceptional audited support rename retains the previous slug as an alias.
