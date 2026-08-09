---
name: test-dotnet-feature
description: Design, implement, run, and assess risk-based .NET tests using repository evidence and the repository's normative .NET 10 engineering handbook. Use for testing features, fixes, contracts, persistence, integrations, security, operations, or deployment behavior at the narrowest realistic boundary.
---

# Test a .NET Feature

## Establish context

Treat [`dotnet10-engineering-standards/`](../../../dotnet10-engineering-standards/) as normative guidance. Before acting, verify the folder and every mandatory or selected reference exists. If anything is missing, stop and report its exact unresolved path.

Always read completely:

- [`README.md`](../../../dotnet10-engineering-standards/README.md)
- [`SKILL-USAGE-GUIDE.md`](../../../dotnet10-engineering-standards/SKILL-USAGE-GUIDE.md)
- [`00-foundation/01-engineering-principles.md`](../../../dotnet10-engineering-standards/00-foundation/01-engineering-principles.md)
- [`05-quality/01-testing-strategy.md`](../../../dotnet10-engineering-standards/05-quality/01-testing-strategy.md)
- [`05-quality/02-test-implementation.md`](../../../dotnet10-engineering-standards/05-quality/02-test-implementation.md)
- [`07-workflows/01-feature-workflow.md`](../../../dotnet10-engineering-standards/07-workflows/01-feature-workflow.md)
- [`07-workflows/03-master-checklists.md`](../../../dotnet10-engineering-standards/07-workflows/03-master-checklists.md)
- [`templates/TEST-PLAN.md`](../../../dotnet10-engineering-standards/templates/TEST-PLAN.md)

Inspect acceptance criteria, changed paths and their callers, existing coverage, test projects and fixtures, CI commands, production provider/version, public boundaries, configuration, and deployment assumptions before selecting tests. Identify risk, affected behavior, test data, environment, and evidence gaps. Preserve unrelated user changes.

## Route references

Read every applicable reference completely; combine overlapping routes:

- **Architecture/domain/application:** [`clean architecture`](../../../dotnet10-engineering-standards/01-architecture/01-clean-architecture.md), [`domain/application design`](../../../dotnet10-engineering-standards/01-architecture/02-domain-application-design.md), and [`feature design`](../../../dotnet10-engineering-standards/01-architecture/03-feature-design.md).
- **C# and runtime behavior:** [`C# standard`](../../../dotnet10-engineering-standards/02-csharp/01-csharp14-coding-standard.md), [`async/concurrency/cancellation`](../../../dotnet10-engineering-standards/02-csharp/02-async-concurrency-cancellation.md), and [`errors/nullability`](../../../dotnet10-engineering-standards/02-csharp/03-errors-results-nullability.md) as applicable.
- **MVC:** [`hosting/DI/pipeline`](../../../dotnet10-engineering-standards/03-web/01-hosting-di-request-pipeline.md), [`MVC/Razor`](../../../dotnet10-engineering-standards/03-web/02-mvc-razor-ui.md), and [`validation/errors`](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md).
- **API:** [`HTTP contracts`](../../../dotnet10-engineering-standards/03-web/03-web-api-http-contracts.md), [`validation/errors`](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), and [`OpenAPI/versioning/compatibility`](../../../dotnet10-engineering-standards/03-web/07-openapi-versioning-compatibility.md).
- **Authentication, authorization, or security:** [`authentication/authorization`](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md) and [`web/API security`](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md).
- **EF Core, database behavior, queries, schema, or migrations:** always read all four data files: [`modeling`](../../../dotnet10-engineering-standards/04-data/01-ef-core-modeling.md), [`querying/performance`](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md), [`transactions/concurrency`](../../../dotnet10-engineering-standards/04-data/03-transactions-concurrency.md), and [`migrations/lifecycle`](../../../dotnet10-engineering-standards/04-data/04-migrations-data-lifecycle.md).
- **Integrations:** [`integrations/resilience`](../../../dotnet10-engineering-standards/03-web/08-integrations-resilience.md).
- **Messaging or background/realtime work:** [`distributed boundaries/messaging`](../../../dotnet10-engineering-standards/01-architecture/04-distributed-boundaries-messaging.md) and [`background jobs/realtime`](../../../dotnet10-engineering-standards/03-web/09-background-jobs-realtime.md).
- **Configuration and observability:** [`configuration/secrets`](../../../dotnet10-engineering-standards/06-operations/01-configuration-secrets-environments.md) and [`observability/health`](../../../dotnet10-engineering-standards/06-operations/02-observability-health.md).
- **Deployment and release:** [`CI/CD/supply chain`](../../../dotnet10-engineering-standards/06-operations/03-ci-cd-supply-chain.md), [`deployment/release/rollback`](../../../dotnet10-engineering-standards/06-operations/04-deployment-release-rollback.md), and [`production readiness`](../../../dotnet10-engineering-standards/06-operations/05-containers-hosting-production-readiness.md).
- **Any non-functional risk:** always read [`performance/reliability testing`](../../../dotnet10-engineering-standards/05-quality/04-performance-reliability-testing.md), plus [`.NET performance/resources`](../../../dotnet10-engineering-standards/02-csharp/04-performance-resource-management.md) and [`query performance`](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md) when applicable. If persistence is involved, still read all four data files.

## Build the risk matrix

Before substantial test implementation, produce a prioritized matrix with risk, scenario, level, environment/provider, setup/data, expected observable evidence, and automation status. Cover applicable success and regression paths plus validation, boundaries, malformed/oversized input, authentication, authorization, ownership, tenant isolation, missing/deleted state, conflicts, duplicates, concurrency, idempotency, cancellation, timeouts, dependency rejection, partial failure, restart, serialization, status/headers, pagination/order, data constraints, compatibility, and sensitive-data exposure. Use equivalence classes and risk; do not exhaustively combine low-value permutations or equate coverage percentage with confidence.

Select the narrowest realistic boundary:

- Use **unit tests** for pure business decisions, calculations, invariants, and state transitions.
- Use **integration tests** for EF Core and the production provider, authentication, authorization handlers, serialization, middleware, dependency injection, and infrastructure adapters.
- Use **HTTP tests** for routing, binding, validation, status codes, headers, Problem Details, antiforgery, authentication/authorization wiring, redirects, and the complete request pipeline.
- Use **contract tests** for public or external schemas, semantics, fixtures, consumer expectations, and compatibility.
- Use **end-to-end tests** only for critical journeys or deployed integration behavior not proven reliably at narrower boundaries.

Avoid deep framework mocks, mocked `DbSet`/`IQueryable`, mocked middleware pipelines, and EF InMemory or SQLite as substitutes for provider-specific relational behavior. Use the real production database provider/version for translation, constraints, transactions, concurrency, and migrations. Use controlled handlers, test servers, containers, sandboxes, or narrow fakes at genuine external boundaries.

## Implement and execute

Follow existing test framework, naming, fixtures, categories, and CI conventions. Keep tests deterministic, isolated, parallel-safe, and diagnosable. Control clocks, randomness, culture, time zones, identities, and external responses; bound waits and use explicit synchronization. Isolate and clean data/resources without hiding primary failures. Never use live production dependencies, credentials, secrets, or personal data.

Assert observable outcomes, persisted state, authorization/data exposure, durable side effects, contracts, and required telemetry rather than private methods or incidental call order. Add a failing regression test before a bug fix when practical. Do not change production behavior or introduce abstractions solely to simplify tests.

Run focused commands first, then broaden by risk: affected test filters/projects, provider integration, HTTP/contract/security tests, affected builds, solution tests, analyzers, migrations, publish/container smoke, and performance/reliability checks as applicable. Never claim an unexecuted, failed, or unavailable check passed.

## Assess and report

Compare results with acceptance criteria and matrix priorities. Separate change-caused failures from pre-existing or environmental failures. Report tests added or changed, exact commands and results, defects found, skipped/blocked cases, retained evidence, and residual risk. For medium/high risk, structure the report or artifact with the [`TEST-PLAN`](../../../dotnet10-engineering-standards/templates/TEST-PLAN.md) template.
