---
name: implement-dotnet-feature
description: Implement complete production-quality C# and .NET 10 features in an existing repository. Use to build, add, modify, fix, or refactor ASP.NET Core MVC or API behavior, EF Core persistence, background jobs, integrations, domain logic, application use cases, configuration, or observability while preserving repository conventions and verifying the vertical result.
---

# Implement .NET Feature

Deliver the smallest complete vertical behavior change within the authorized scope. Do not stop at scaffolding, disconnected snippets, or TODOs when a safe complete path is possible.

## Resolve the standards

Locate the repository root first. The standards directory MUST exist at:

`<repository-root>/dotnet10-engineering-standards`

This skill is expected at `.agents/skills/implement-dotnet-feature/SKILL.md`, so its standards links use `../../../dotnet10-engineering-standards/`.

If the standards directory or a required reference is missing, stop and report the exact missing path. Do not silently substitute generic guidance.

## Operating sequence

1. Inspect repository instructions, worktree state, solution/project files, configuration, nearby vertical features, tests, and CI/deployment assumptions before editing.
2. Confirm observable outcome and acceptance criteria. Ask only when a missing decision materially changes behavior, security, data, compatibility, or destructive impact.
3. Trace the existing request/event path and identify affected boundaries.
4. Read `CORE.md` and only the conditional references required by the affected boundaries.
5. Choose the simplest design that meets the requirement and fits sound local conventions.
6. Implement a cohesive vertical path: owning business rules, application orchestration, infrastructure adapter, presentation mapping, tests, and operational changes as applicable.
7. Verify incrementally, starting with focused build/tests and expanding according to blast radius.
8. Inspect the final diff for accidental scope, unrelated formatting, unsafe contract/data changes, missing files, secrets, and incomplete behavior.

## Mandatory references

Read this file completely for every implementation:

- [Engineering core](../../../dotnet10-engineering-standards/CORE.md)

## Conditional reference routing

- Domain rules, entities, value objects, invariants, state changes, commands, or queries: [Domain and application design](../../../dotnet10-engineering-standards/01-architecture/02-domain-application-design.md).
- Async I/O, parallel work, cancellation, streams, or producer/consumer behavior: [Async, concurrency, and cancellation](../../../dotnet10-engineering-standards/02-csharp/02-async-concurrency-cancellation.md).
- Evidence-based allocation, serialization, cache, connection, or throughput work: [Performance and resource management](../../../dotnet10-engineering-standards/02-csharp/04-performance-resource-management.md).
- MVC/Razor behavior: [MVC and Razor UI](../../../dotnet10-engineering-standards/03-web/02-mvc-razor-ui.md). Add [pipeline](../../../dotnet10-engineering-standards/03-web/01-hosting-di-request-pipeline.md), [validation](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), [authentication/authorization](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md), or [web security](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md) only when that concern changes.
- HTTP API contract: [API contracts](../../../dotnet10-engineering-standards/03-web/03-web-api-http-contracts.md). Add [validation](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), [authentication/authorization](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md), [web security](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md), or [compatibility](../../../dotnet10-engineering-standards/03-web/07-openapi-versioning-compatibility.md) only when that concern changes.
- EF Core: select only the affected data concern—[modeling](../../../dotnet10-engineering-standards/04-data/01-ef-core-modeling.md), [querying](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md), [transactions/concurrency](../../../dotnet10-engineering-standards/04-data/03-transactions-concurrency.md), or [migrations/lifecycle](../../../dotnet10-engineering-standards/04-data/04-migrations-data-lifecycle.md). Verify provider-specific behavior against the production provider.
- External HTTP/vendor integration: [Integrations and resilience](../../../dotnet10-engineering-standards/03-web/08-integrations-resilience.md).
- Events, queues, outbox/inbox, distribution, or multi-tenancy: [Distributed boundaries and messaging](../../../dotnet10-engineering-standards/01-architecture/04-distributed-boundaries-messaging.md).
- Background jobs, schedulers, hosted services, or SignalR: [Background jobs and realtime](../../../dotnet10-engineering-standards/03-web/09-background-jobs-realtime.md), plus distributed boundaries.
- Configuration, secrets, flags, connection strings, certificates, or keys: [Configuration and secrets](../../../dotnet10-engineering-standards/06-operations/01-configuration-secrets-environments.md).
- Logs, metrics, traces, audit events, or health checks: [Observability and health](../../../dotnet10-engineering-standards/06-operations/02-observability-health.md).
- Test-boundary or coverage decision: [Testing strategy](../../../dotnet10-engineering-standards/05-quality/01-testing-strategy.md). Complex fixture, provider, or test implementation: [Test implementation](../../../dotnet10-engineering-standards/05-quality/02-test-implementation.md).
- CI or packaging: [CI/CD and supply chain](../../../dotnet10-engineering-standards/06-operations/03-ci-cd-supply-chain.md). Deployment, migration ordering, feature flag, or rollback: [Deployment and rollback](../../../dotnet10-engineering-standards/06-operations/04-deployment-release-rollback.md).
- Defect, schema, integration, authentication, contract, performance, upgrade, or hotfix work: the applicable section of [Change playbooks](../../../dotnet10-engineering-standards/07-workflows/02-change-playbooks.md).

## Implementation rules

- Preserve unrelated user work and keep the diff focused. Do not broadly reformat or restructure the repository.
- Keep business invariants in the domain/application owner; keep controllers/endpoints thin and adapters technical.
- Use explicit request/response DTOs and ViewModels. Never bind EF entities directly.
- Validate untrusted transport input and independently enforce authoritative business invariants.
- Enforce role/policy plus resource ownership or tenant scope server-side where applicable.
- Propagate `CancellationToken` through meaningful I/O. Avoid sync-over-async and unowned fire-and-forget work.
- Define atomic transaction boundaries and safe concurrency/idempotency behavior for retries, jobs, messages, webhooks, and duplicate requests.
- Bound result sets, payloads, retries, parallelism, and external calls. Use timeouts and retry only transient, idempotent operations.
- Review generated migrations for destructive changes, provider behavior, mixed-version compatibility, locks, and recovery.
- Use structured contextual logging at useful boundaries without duplicate exception logs or sensitive data.
- Preserve backward compatibility unless a breaking change is explicitly approved and migration-safe.
- Add an abstraction only for a concrete boundary, meaningful variation, or duplication whose removal improves the design.

## Verification

Use repository commands when present. Otherwise, adapt this sequence to the affected projects:

1. Restore if dependencies changed.
2. Build the affected project in Release configuration.
3. Run focused unit/component tests.
4. Run provider integration and HTTP/contract tests when applicable.
5. Run broader solution build/tests based on blast radius.
6. Run analyzers, formatting checks, dependency/security audit, migration checks, publish/container/runtime smoke when relevant.

Never claim an unrun check passed. Distinguish failures introduced by the change from pre-existing or environment failures with evidence.

## Required report

State the implemented outcome first, important design decisions, affected files/components, exact commands and results, migration/configuration/release notes, and any material unverified risk or follow-up.
