---
status: accepted
---

# Store attachments in Blob Storage

ERP documents and attachments use object-storage semantics: Azure Blob Storage in deployed Azure environments and Azurite Blob locally through Aspire. The owning module stores business ownership, authorization, content metadata, checksum, retention state, and audit facts in its PostgreSQL schema; object storage holds the bytes and is never treated as the authorization model. Azure Files and mounted filesystem semantics are excluded unless a future integration demonstrates that they are required.
