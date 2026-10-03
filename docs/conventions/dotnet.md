# .NET conventions

## Enum values

Assign every enum member an explicit numeric value, starting at 1. Reserve 0 for invalid
or uninitialized data; it must not represent a valid domain state, including anonymity.
Keep assigned numbers stable when adding or reordering members. Changing persisted numbers
requires an explicit data migration.

Explicit numbering does not prevent a cast or deserializer from producing 0 or another
undefined value. Validate enum values at external-input and persistence boundaries before
accepting them as valid application state. The context factories expose named actor
construction rather than accepting a caller-supplied enum value.
