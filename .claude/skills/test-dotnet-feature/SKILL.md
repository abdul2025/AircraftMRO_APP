---
name: test-dotnet-feature
description: Design, add, run, or assess risk-based tests for a C#/.NET feature or defect. Use when behavioral confidence, regression coverage, or test gaps must be established.
---

# Test .NET Feature

Build confidence in observable behavior with the smallest realistic test set that covers
material risk. The user's request controls the mode: analysis is read-only; adding tests
authorizes test-file changes only; production changes require separate authorization.

## Baseline

Read [Engineering core](../../../dotnet10-engineering-standards/CORE.md). If it is
missing, report the path and continue using repository test conventions and evidence.
Also read the repository's
[Application feature pattern](../../../dotnet10-engineering-standards/FEATURE-PATTERN.md)
so tests exercise the service, canonical result, domain behavior, and Infrastructure
query/persistence boundaries that the feature actually uses. When guidance conflicts,
follow the priority order in the
[standards README](../../../dotnet10-engineering-standards/README.md#authority).

## Work

1. Inspect requirements, acceptance criteria, the relevant diff/path, existing tests,
   test projects, fixtures, CI commands, and production providers or integrations.
2. Map changed behavior to credible risks and identify what existing tests already prove.
3. Choose the narrowest boundary that can reveal each risk without mocking it away:
   unit tests for pure decisions; integration tests for framework/provider/infrastructure
   behavior; HTTP tests for complete request wiring; contract or end-to-end tests only
   when lower levels cannot prove the risk.
4. Prioritize happy-path completion, important validation and authorization failures,
   data/concurrency behavior, dependency failure, cancellation/timeouts, compatibility,
   and the exact regression scenario when applicable.
5. When test implementation is requested, follow repository conventions, keep tests
   deterministic and isolated, and assert outcomes rather than incidental call counts.
6. Run focused tests first, then broader suites according to blast radius. Investigate
   unexpected passes, flakiness, shared state, and environment-only failures.

Use the real production database provider, serializer, authentication pipeline, or other
boundary when the behavior under test depends on it. Do not deep-mock the very behavior
the test is meant to verify.

## Output

Return:

1. Risk and existing-coverage summary
2. Prioritized scenarios with level, setup, action, and expected outcome
3. Tests added or run, including exact files and commands
4. Results, failures, flakiness, and environment limits
5. Intentionally excluded scenarios and remaining material risk

Never equate coverage percentage with correctness and never claim an unrun check passed.
