---
name: plan-dotnet-feature
description: Plan an implementation-ready C#/.NET feature from repository evidence without changing files. Use when scope, design, affected code, risks, or verification must be decided before implementation.
---

# Plan .NET Feature

Produce a repository-grounded plan. This is a read-only mode: do not modify production
code, tests, configuration, migrations, or deployment files.

## Baseline

Read [Engineering core](../../../dotnet10-engineering-standards/CORE.md). If it is
missing, report the path and continue using repository instructions and available
evidence; do not invent repository facts. Also read the repository's
[Application feature pattern](../../../dotnet10-engineering-standards/FEATURE-PATTERN.md)
and make the plan conform to it unless an exception is explicitly approved. When guidance
conflicts, follow the priority order in the
[standards README](../../../dotnet10-engineering-standards/README.md#authority).

## Work

1. Inspect repository instructions, the solution/projects, target behavior,
   configuration, relevant tests, and one nearby comparable path when available.
2. Trace the current behavior through the affected boundaries. Record exact files and
   symbols that support the plan.
3. State the desired outcome, acceptance criteria, scope, non-goals, constraints,
   assumptions, and decisions that genuinely remain open.
4. Select the simplest complete design that fits sound local conventions. Explain only
   material choices and tradeoffs.
5. Identify contract, authorization, data, concurrency, failure, compatibility,
   observability, migration, and rollout effects only where applicable.
6. Create an ordered implementation sequence. Each step must name its likely location,
   intended change, dependency on earlier steps, and how completion will be verified.
7. Define a risk-based test strategy and any checks needed before release.

Ask the user only when an unresolved choice would materially change the plan. Otherwise,
make a clearly labeled reasonable assumption.

## Output

Return:

1. Outcome and acceptance criteria
2. Current path and repository evidence
3. Proposed design and affected boundaries
4. Ordered file/symbol-level implementation steps
5. Verification and test strategy
6. Risks, assumptions, and open decisions

Do not produce generic phase labels without concrete repository locations and behavior.
Do not claim that an unrun check passed.
