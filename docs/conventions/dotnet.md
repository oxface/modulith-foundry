# .NET conventions

## Statement spacing

Use a blank line after initial argument/exception guards, before the main operation.
Separate a completed control-flow block from the next independent statement with a blank
line, including after loops. Keep connected clauses (`else`, `catch`, `finally`) together;
do not add a blank line before the enclosing block's closing brace or a connected delimiter.
Use the same separation after an unbraced guard that throws or returns.

CSharpier formats syntax and preserves these statement-level separators, but its configuration
does not provide an option to insert them automatically. Apply this convention when editing
code; it does not authorize repository-wide reformatting.

## Enum values

Assign every enum member an explicit numeric value, starting at 1. Reserve 0 for invalid
or uninitialized data; it must not represent a valid domain state, including anonymity.
Keep assigned numbers stable when adding or reordering members. Changing persisted numbers
requires an explicit data migration.

Explicit numbering does not prevent a cast or deserializer from producing 0 or another
undefined value. Validate enum values at external-input and persistence boundaries before
accepting them as valid application state. The context factories expose named actor
construction rather than accepting a caller-supplied enum value.
