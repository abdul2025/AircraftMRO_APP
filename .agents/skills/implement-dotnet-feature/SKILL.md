---
name: implement-dotnet-feature
description: Implement complete vertical .NET features using repository evidence and the repository's normative .NET 10 engineering handbook. Use when Codex is asked to add, change, or fix application behavior across applicable domain, application, infrastructure, presentation, persistence, tests, and operations.
---

# Implement a .NET Feature

## Establish context

Treat [`dotnet10-engineering-standards/`](../../../dotnet10-engineering-standards/) as normative guidance. Before editing, verify the folder and every required or selected reference exists. If anything is missing, stop and report its exact unresolved path.

Always read completely:

- [`README.md`](../../../dotnet10-engineering-standards/README.md)
- [`SKILL-USAGE-GUIDE.md`](../../../dotnet10-engineering-standards/SKILL-USAGE-GUIDE.md)
- [`00-foundation/01-engineering-principles.md`](../../../dotnet10-engineering-standards/00-foundation/01-engineering-principles.md)
- [`01-architecture/01-clean-architecture.md`](../../../dotnet10-engineering-standards/01-architecture/01-clean-architecture.md)
- [`01-architecture/03-feature-design.md`](../../../dotnet10-engineering-standards/01-architecture/03-feature-design.md)
- [`02-csharp/01-csharp14-coding-standard.md`](../../../dotnet10-engineering-standards/02-csharp/01-csharp14-coding-standard.md)
- [`02-csharp/03-errors-results-nullability.md`](../../../dotnet10-engineering-standards/02-csharp/03-errors-results-nullability.md)
- [`07-workflows/01-feature-workflow.md`](../../../dotnet10-engineering-standards/07-workflows/01-feature-workflow.md)
- [`07-workflows/02-change-playbooks.md`](../../../dotnet10-engineering-standards/07-workflows/02-change-playbooks.md)

Inspect before editing: repository instructions and worktree state; solution/project files, frameworks, packages, analyzers, and build commands; relevant implementation and one comparable vertical feature; tests; configuration and dependency registration; ADRs and boundaries; persistence and migrations; CI/CD, hosting, and deployment assumptions. Trace current behavior, restate acceptance criteria and risks, and preserve unrelated user changes.

## Route additional references

Read every applicable reference completely; combine routes when concerns overlap:

- **MVC:** [`hosting/DI/pipeline`](../../../dotnet10-engineering-standards/03-web/01-hosting-di-request-pipeline.md), [`MVC/Razor`](../../../dotnet10-engineering-standards/03-web/02-mvc-razor-ui.md), and [`validation/errors`](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md).
- **API:** [`HTTP contracts`](../../../dotnet10-engineering-standards/03-web/03-web-api-http-contracts.md), [`validation/errors`](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), and [`OpenAPI/versioning/compatibility`](../../../dotnet10-engineering-standards/03-web/07-openapi-versioning-compatibility.md).
- **Authentication, authorization, tenant, or ownership:** [`authentication/authorization`](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md).
- **Security, privacy, uploads, or exposed input:** [`web/API security`](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md).
- **Async, parallel, or cancellable work:** [`async/concurrency/cancellation`](../../../dotnet10-engineering-standards/02-csharp/02-async-concurrency-cancellation.md).
- **Persistence, EF Core, queries, schema, or migrations:** always read all four data files: [`modeling`](../../../dotnet10-engineering-standards/04-data/01-ef-core-modeling.md), [`querying/performance`](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md), [`transactions/concurrency`](../../../dotnet10-engineering-standards/04-data/03-transactions-concurrency.md), and [`migrations/lifecycle`](../../../dotnet10-engineering-standards/04-data/04-migrations-data-lifecycle.md). Verify provider-specific behavior with the real provider.
- **External integrations:** [`integrations/resilience`](../../../dotnet10-engineering-standards/03-web/08-integrations-resilience.md), plus API, security, async, observability, and testing references as applicable.
- **Messaging or distributed boundaries:** [`distributed boundaries/messaging`](../../../dotnet10-engineering-standards/01-architecture/04-distributed-boundaries-messaging.md).
- **Background jobs or SignalR/realtime:** [`background jobs/realtime`](../../../dotnet10-engineering-standards/03-web/09-background-jobs-realtime.md), plus messaging, data, and observability references as applicable.
- **Configuration or secrets:** [`configuration/secrets/environments`](../../../dotnet10-engineering-standards/06-operations/01-configuration-secrets-environments.md).
- **Observability or health:** [`observability/health`](../../../dotnet10-engineering-standards/06-operations/02-observability-health.md).
- **Testing:** [`testing strategy`](../../../dotnet10-engineering-standards/05-quality/01-testing-strategy.md) and [`test implementation`](../../../dotnet10-engineering-standards/05-quality/02-test-implementation.md); add [`performance/reliability testing`](../../../dotnet10-engineering-standards/05-quality/04-performance-reliability-testing.md) for non-functional risk.
- **CI/CD or supply chain:** [`CI/CD/supply chain`](../../../dotnet10-engineering-standards/06-operations/03-ci-cd-supply-chain.md).
- **Deployment, rollout, rollback, containers, or hosting:** [`deployment/release/rollback`](../../../dotnet10-engineering-standards/06-operations/04-deployment-release-rollback.md) and [`production readiness`](../../../dotnet10-engineering-standards/06-operations/05-containers-hosting-production-readiness.md).
- **Performance or capacity:** [`.NET performance/resources`](../../../dotnet10-engineering-standards/02-csharp/04-performance-resource-management.md), [`query performance`](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md), and [`performance/reliability testing`](../../../dotnet10-engineering-standards/05-quality/04-performance-reliability-testing.md). If persistence changes, still read all four data files.

## Implement the vertical behavior

Implement the smallest complete vertical feature that satisfies current acceptance criteria. Preserve sound repository conventions; do not perform unrelated refactoring. Avoid new layers, interfaces, CQRS, MediatR, repositories, queues, caches, events, packages, or service splits unless a concrete current need and repository evidence justify their costs.

Cover applicable domain/application rules, infrastructure adapters, presentation, persistence/schema, tests, configuration, telemetry, deployment, and documentation. Keep each responsibility at its boundary:

- Make business invariants authoritative in domain or application code so other callers cannot bypass them.
- Keep controllers/endpoints thin: authorize, bind a purpose-built DTO/ViewModel, invoke the use case, and map the result. Never bind persistence entities.
- Validate transport input and expected business outcomes explicitly. Centralize unexpected error handling and preserve truthful nullability.
- Enforce authentication and server-side authorization, including resource, tenant, and ownership scope in data access.
- Pass cancellation end to end. Bound request sizes, collections, pagination, outbound I/O, timeouts, retries, parallelism, and resource use.
- Give one use case clear transaction ownership. Define concurrency, duplicate delivery, idempotency, side-effect ordering, and recovery behavior where relevant.
- Preserve public API, schema, event, configuration, and mixed-version compatibility or deliberately version and migrate them.
- Log safely and once at the handling boundary. Exclude secrets, tokens, sensitive payloads, and personal data; add useful traces, metrics, health behavior, and runbook implications for new operational failure modes.
- Preserve unrelated work and keep every intermediate state safe and reviewable. Stop for a missing choice only when it materially changes correctness or an irreversible contract/data decision.

## Verify by risk

Run repository-native checks, focused first:

1. Build the affected project or compile the narrowest target.
2. Run focused unit/application/component tests.
3. Run real-provider integration or migration tests when persistence is affected.
4. Run MVC/API/contract/security tests for changed boundaries.
5. Run broader solution build and tests according to blast radius.
6. Run applicable analyzers, formatting, security, compatibility, publish/container, migration, performance, and runtime smoke checks.

Add tests for observable success, validation, authorization and wrong tenant/owner, important state boundaries, concurrency/duplicates, cancellation, dependency failures, compatibility, and sensitive-data absence according to risk. Do not distort production design merely to ease mocking. Never claim an unexecuted or failed check passed.

## Report

State the implemented outcome, important decisions, affected files and boundaries, migration/configuration/release notes, and exact verification commands with their results. Clearly identify skipped, unavailable, pre-existing, or failing checks and remaining material risks or follow-ups.
