---
name: implement-dotnet-feature
description: Implement or fix a complete C#/.NET feature in an existing repository. Use when the user authorizes code changes and expects working behavior with verification.
---

# Implement .NET Feature

Deliver the smallest complete vertical behavior change within the user's authorized
scope. Do not stop at scaffolding or disconnected snippets when the repository permits a
safe complete implementation.

## Baseline

Read [Engineering core](../../../dotnet10-engineering-standards/CORE.md). If it is
missing, report the path and continue cautiously using repository instructions rather
than blocking unrelated work. Also read and apply the repository's
[Application feature pattern](../../../dotnet10-engineering-standards/FEATURE-PATTERN.md)
for every new or materially changed feature unless the user explicitly approves an
exception. When guidance conflicts, follow the priority order in the
[standards README](../../../dotnet10-engineering-standards/README.md#authority).

## Work

1. Inspect repository instructions, worktree state, solution/projects, nearby behavior,
   tests, configuration, and build commands before editing.
2. Confirm the observable outcome and acceptance criteria. Ask only when a missing
   decision materially affects behavior, security, data, compatibility, or destructive
   impact.
3. Trace the current path and choose the smallest design that fits sound repository
   conventions.
4. Implement the complete affected path: business behavior, boundaries, persistence or
   integrations, presentation mapping, tests, configuration, and telemetry only as
   applicable.
5. Verify incrementally: focused build/tests first, then broader checks according to the
   change's risk and blast radius.
6. Inspect the final diff for incomplete behavior, accidental scope, unsafe contract or
   data changes, secrets, generated migration issues, and unrelated formatting.

Preserve unrelated user changes. Do not introduce patterns or abstractions merely to
match a theoretical architecture. Do not mutate production or other live environments
without explicit authorization for that target.

## Output

Lead with the implemented outcome, then report:

- important design decisions;
- changed files/components;
- exact verification commands and results;
- migration, configuration, or release effects; and
- material limitations or unverified risks.

Never claim an unrun check passed, and distinguish change-related failures from
pre-existing or environmental failures when evidence permits.
