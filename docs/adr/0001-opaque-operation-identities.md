# Use opaque application keys for operation context

Operation context uses distinct tenant and actor identities carrying opaque string keys,
with actor kind distinguishing human and system identities; this supports consumer-owned
identity schemes without spreading generic key parameters through dependent libraries.
Consumers explicitly map domain IDs and external identities: GUID-only keys would freeze
the sample's representation, while provider lookup and email-based linking would introduce
Access policy into a technical library.
Keys compare ordinally and preserve accepted text, leaving canonicalization and the
application identity namespace with the consumer.
