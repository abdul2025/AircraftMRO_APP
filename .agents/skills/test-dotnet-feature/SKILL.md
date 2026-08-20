---
name: test-dotnet-feature
description: Design, implement, run, and assess risk-based tests for C# and .NET 10 features and fixes. Use for ASP.NET Core MVC or APIs, EF Core/provider behavior, domain and application logic, authentication/authorization, middleware, background jobs, integrations, contracts, migrations, performance, reliability, and regressions. Select realistic boundaries and prioritize behavioral confidence over test count or coverage percentage.
---

# Test .NET Feature

Build confidence in observable behavior with the smallest test set that covers meaningful risk. Do not distort production design merely to make mocking convenient.

## Resolve the standards

Locate the repository root first. The standards directory MUST exist at:

`<repository-root>/dotnet10-engineering-standards`

This skill is expected at `.agents/skills/test-dotnet-feature/SKILL.md`, so its standards links use `../../../dotnet10-engineering-standards/`.

If the standards directory or a required reference is missing, stop and report the exact missing path.

## Operating sequence

1. Inspect repository instructions, feature requirements, acceptance criteria, change diff, nearby tests, test projects, fixtures, CI commands, production provider, and public/runtime boundaries.
2. Trace each affected path and inventory risks by impact, likelihood, detectability, and reversibility.
3. Inspect existing coverage and distinguish genuine behavioral gaps from already-proven behavior.
4. Read `CORE.md`, the testing reference, and only the affected-topic references needed for the risks under test.
5. Select the narrowest realistic boundary that can expose each risk without mocking away the behavior under test.
6. Produce a prioritized test matrix before implementing substantial new coverage.
7. When implementation is requested, add focused tests using repository conventions, then run focused and broader suites according to blast radius.
8. Investigate unexpected passes, nondeterminism, shared-state leakage, and environment-only failures.

## Mandatory references

Read these files completely for every testing task:

- [Engineering core](../../../dotnet10-engineering-standards/CORE.md)
- [Testing strategy](../../../dotnet10-engineering-standards/05-quality/01-testing-strategy.md)

Read [Performance and reliability testing](../../../dotnet10-engineering-standards/05-quality/04-performance-reliability-testing.md) when latency, throughput, memory, contention, capacity, retry storms, failover, soak behavior, or another non-functional risk exists.

## Affected-topic routing

- Domain invariants, calculations, state transitions, and application decisions: [Domain and application design](../../../dotnet10-engineering-standards/01-architecture/02-domain-application-design.md).
- Nullability, errors, async, cancellation, time, culture, concurrency, or resource behavior: select only [coding](../../../dotnet10-engineering-standards/02-csharp/01-csharp14-coding-standard.md), [async/concurrency](../../../dotnet10-engineering-standards/02-csharp/02-async-concurrency-cancellation.md), [errors/nullability](../../../dotnet10-engineering-standards/02-csharp/03-errors-results-nullability.md), or [performance/resources](../../../dotnet10-engineering-standards/02-csharp/04-performance-resource-management.md) as applicable.
- MVC/Razor behavior: [MVC and Razor UI](../../../dotnet10-engineering-standards/03-web/02-mvc-razor-ui.md). Add [validation](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), [authentication/authorization](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md), or [web security](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md) only when those behaviors are under test.
- API behavior: [API contracts](../../../dotnet10-engineering-standards/03-web/03-web-api-http-contracts.md). Add [validation](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), [authentication/authorization](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md), [web security](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md), or [compatibility](../../../dotnet10-engineering-standards/03-web/07-openapi-versioning-compatibility.md) only when those behaviors are under test.
- EF Core: select only [modeling/provider mapping](../../../dotnet10-engineering-standards/04-data/01-ef-core-modeling.md), [querying](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md), [transactions/concurrency](../../../dotnet10-engineering-standards/04-data/03-transactions-concurrency.md), or [migrations/lifecycle](../../../dotnet10-engineering-standards/04-data/04-migrations-data-lifecycle.md) as applicable. Use the production provider or a faithful provider boundary for provider-dependent behavior.
- External integrations and resilience: [Integrations and resilience](../../../dotnet10-engineering-standards/03-web/08-integrations-resilience.md).
- Events, queues, jobs, scheduling, duplicate delivery, or SignalR: [Distributed boundaries](../../../dotnet10-engineering-standards/01-architecture/04-distributed-boundaries-messaging.md) and [Background jobs/realtime](../../../dotnet10-engineering-standards/03-web/09-background-jobs-realtime.md).
- Configuration, telemetry, CI, deployment, container, or runtime smoke tests: select only [configuration/secrets](../../../dotnet10-engineering-standards/06-operations/01-configuration-secrets-environments.md), [observability/health](../../../dotnet10-engineering-standards/06-operations/02-observability-health.md), [CI/CD](../../../dotnet10-engineering-standards/06-operations/03-ci-cd-supply-chain.md), [deployment/rollback](../../../dotnet10-engineering-standards/06-operations/04-deployment-release-rollback.md), or [containers/hosting](../../../dotnet10-engineering-standards/06-operations/05-containers-hosting-production-readiness.md) as applicable.
- Adding or substantially restructuring tests: [Test implementation](../../../dotnet10-engineering-standards/05-quality/02-test-implementation.md).
- Defect, schema, integration, authentication, contract, performance, upgrade, or hotfix scenario: the applicable section of [Change playbooks](../../../dotnet10-engineering-standards/07-workflows/02-change-playbooks.md).
- Formal test plan requested: [Test plan template](../../../dotnet10-engineering-standards/templates/TEST-PLAN.md).

## Select the boundary

- Use unit tests for pure domain invariants, calculations, policies, branching, and application decisions without real infrastructure behavior.
- Use integration tests for EF Core mappings/queries/transactions, production-provider behavior, authentication, authorization, serialization, middleware, DI wiring, caching, files, and adapters.
- Use HTTP/functional tests for routing, model binding, filters, status codes, headers, content negotiation, Problem Details, antiforgery, and end-to-end use-case wiring.
- Use contract tests for external payloads/events/APIs whose shape and compatibility matter.
- Use end-to-end tests sparingly for critical journeys that lower levels cannot prove.
- Use performance/load/soak/fault tests only with an explicit risk, workload, baseline, target, and reproducible environment.

Do not create a unit test merely because a class exists. Do not deep-mock EF Core, ASP.NET Core, authentication, serialization, or provider behavior when those are the risks being verified.

## Risk checklist

Cover only applicable risks, prioritizing high impact:

- happy path and observable business completion;
- invalid, missing, boundary, malformed, oversized, and over-posted input;
- anonymous, invalid identity, forbidden role/policy, permitted user, wrong owner/resource, and cross-tenant access;
- not-found, conflict, duplicate, replay, stale version, concurrency, and idempotency;
- transaction rollback, partial side effects, cancellation, timeout, dependency failure, retry, restart, and shutdown;
- serialization, null/omitted values, enums, dates/time zones, culture, money/precision, and compatibility;
- deterministic pagination, filtering, sorting, large input, query count, and provider constraints;
- background-job duplicate execution, overlap, retry exhaustion, dead-letter/replay, scope, and graceful shutdown;
- exact regression scenario for a fixed defect.

## Test implementation rules

- Follow the repository's existing framework, naming, fixture, assertion, and data-isolation conventions.
- Make tests deterministic, independent, readable, parallel-safe unless explicitly serialized, and free of real secrets/PII/live dependencies.
- Control time, randomness, identifiers, and external I/O through appropriate seams.
- Assert meaningful output, contract, persisted state, emitted side effect, and security outcome—not incidental call counts.
- Use realistic builders/fixtures without hiding the scenario inside excessive abstraction.
- Avoid arbitrary delays, shared mutable state, order dependence, and replacing production-provider behavior with an incompatible in-memory substitute.

## Required output

Provide:

1. Acceptance-criteria and changed-path risk map
2. Prioritized test matrix with scenario, risk, level, setup, expected result, and priority
3. Existing coverage and genuine gaps
4. Tests added or run with exact file names and commands
5. Results, failures, flakiness, and environment limitations
6. Intentionally excluded scenarios with rationale
7. Remaining material risk

Never claim a feature is fully tested because coverage percentage increased, and never claim an unrun check passed.
