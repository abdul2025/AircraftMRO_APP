---
name: review-dotnet-feature
description: Review a C#/.NET diff, branch, commit, or feature for concrete defects and missing risk coverage. Use for evidence-backed review; do not edit files unless fixes are separately requested.
---

# Review .NET Feature

Review changed .NET code for defects affecting correctness, security, data, compatibility,
reliability, or operations. Do not modify files unless fixes are separately requested.

## Baseline

Read [Engineering core](../../../dotnet10-engineering-standards/CORE.md) and
[Application feature pattern](../../../dotnet10-engineering-standards/FEATURE-PATTERN.md).
If either is missing, report it and continue using repository evidence. Do not report
untouched legacy structure unless the change makes it unsafe. When guidance conflicts,
follow the priority order in the
[standards README](../../../dotnet10-engineering-standards/README.md#authority).

## Review

1. Determine the intended behavior and exact comparison range.
2. Inspect all changed files and affected callers, contracts, data flows, configuration,
   migrations, tests, and runtime paths.
3. Review correctness, security, data integrity, compatibility, failure handling,
   concurrency, tests, operations, and evidenced performance risks.
4. Run focused, non-mutating checks when useful.

Report a finding only when changed-code evidence shows a realistic trigger and concrete
impact. Exclude style preferences and theoretical concerns.

Severity:

* **Critical:** likely breach, data loss, or severe outage.
* **High:** core behavior, authorization, contract, migration, or major production failure.
* **Medium:** meaningful reliability, edge-case, or operability defect.
* **Low:** limited but concrete impact; never cosmetic.

## Output

List findings by severity. Each finding must include:

* File and line.
* Trigger and failure mechanism.
* Concrete impact.
* **Recommended change:** the specific code, configuration, migration, or test change
  needed to resolve it.

Then provide a prioritized **Recommended Changes** checklist referencing the findings,
followed by open questions, test gaps, checks run, and review limits.

Do not provide patches unless fixes are requested. If there are no findings, say so
plainly and state that no changes are recommended. Never invent findings or claim an
unrun check passed.
