---
name: plan-dotnet-feature
description: Plan .NET features from repository evidence and the repository's normative .NET 10 engineering handbook. Use for feature analysis, scoping, design, or implementation planning when production code must not be changed.
---

# Plan a .NET Feature

Plan only. Never modify application code, tests, configuration, projects, migrations, deployment files, or other production artifacts. Create a plan file only when explicitly requested; otherwise respond with the plan.

## Verify and inspect

Treat [`dotnet10-engineering-standards/`](../../../dotnet10-engineering-standards/) as normative guidance. Before planning, verify the folder and every required or selected reference exists. If anything is missing, stop and report its exact unresolved path.

Always read completely:

- [`README.md`](../../../dotnet10-engineering-standards/README.md)
- [`SKILL-USAGE-GUIDE.md`](../../../dotnet10-engineering-standards/SKILL-USAGE-GUIDE.md)
- [`00-foundation/01-engineering-principles.md`](../../../dotnet10-engineering-standards/00-foundation/01-engineering-principles.md)
- [`01-architecture/01-clean-architecture.md`](../../../dotnet10-engineering-standards/01-architecture/01-clean-architecture.md)
- [`01-architecture/03-feature-design.md`](../../../dotnet10-engineering-standards/01-architecture/03-feature-design.md)
- [`07-workflows/01-feature-workflow.md`](../../../dotnet10-engineering-standards/07-workflows/01-feature-workflow.md)
- [`templates/FEATURE-PLAN.md`](../../../dotnet10-engineering-standards/templates/FEATURE-PLAN.md)

Inspect repository instructions and worktree state; solution and project files; frameworks, packages, and analyzers; relevant implementation and a comparable end-to-end feature; tests; configuration and dependency registration; ADRs and architecture decisions; CI/CD, hosting, and deployment assumptions. Trace actual call paths. Cite repository paths as evidence and distinguish facts, inferences, assumptions, and open questions.

## Route references

Read every applicable reference completely and combine overlapping routes:

- **MVC:** [`hosting/DI/pipeline`](../../../dotnet10-engineering-standards/03-web/01-hosting-di-request-pipeline.md), [`MVC/Razor`](../../../dotnet10-engineering-standards/03-web/02-mvc-razor-ui.md), and [`validation/errors`](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md).
- **API:** [`HTTP contracts`](../../../dotnet10-engineering-standards/03-web/03-web-api-http-contracts.md), [`validation/errors`](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), and [`OpenAPI/versioning`](../../../dotnet10-engineering-standards/03-web/07-openapi-versioning-compatibility.md).
- **Authentication or authorization:** [`authentication/authorization`](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md).
- **Security, privacy, or exposed input:** [`web/API security`](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md); add the [`threat-model template`](../../../dotnet10-engineering-standards/templates/THREAT-MODEL.md) when warranted.
- **Persistence, EF Core, queries, schema, or migrations:** always read all four: [`modeling`](../../../dotnet10-engineering-standards/04-data/01-ef-core-modeling.md), [`querying/performance`](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md), [`transactions/concurrency`](../../../dotnet10-engineering-standards/04-data/03-transactions-concurrency.md), and [`migrations/lifecycle`](../../../dotnet10-engineering-standards/04-data/04-migrations-data-lifecycle.md).
- **Integrations:** [`integrations/resilience`](../../../dotnet10-engineering-standards/03-web/08-integrations-resilience.md), plus API, security, observability, and testing references as applicable.
- **Background jobs, real-time work, or messaging:** [`distributed boundaries`](../../../dotnet10-engineering-standards/01-architecture/04-distributed-boundaries-messaging.md) and [`jobs/realtime`](../../../dotnet10-engineering-standards/03-web/09-background-jobs-realtime.md), plus data and observability references as applicable.
- **Observability or health:** [`observability/health`](../../../dotnet10-engineering-standards/06-operations/02-observability-health.md).
- **Testing:** [`testing strategy`](../../../dotnet10-engineering-standards/05-quality/01-testing-strategy.md) and [`test implementation`](../../../dotnet10-engineering-standards/05-quality/02-test-implementation.md); add [`performance/reliability testing`](../../../dotnet10-engineering-standards/05-quality/04-performance-reliability-testing.md) for non-functional risk.
- **Configuration, deployment, release, or rollback:** [`configuration/secrets`](../../../dotnet10-engineering-standards/06-operations/01-configuration-secrets-environments.md), [`CI/CD`](../../../dotnet10-engineering-standards/06-operations/03-ci-cd-supply-chain.md), [`release/rollback`](../../../dotnet10-engineering-standards/06-operations/04-deployment-release-rollback.md), and [`production readiness`](../../../dotnet10-engineering-standards/06-operations/05-containers-hosting-production-readiness.md).
- **Performance or capacity:** [`.NET performance`](../../../dotnet10-engineering-standards/02-csharp/04-performance-resource-management.md), [`query performance`](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md), and [`performance/reliability testing`](../../../dotnet10-engineering-standards/05-quality/04-performance-reliability-testing.md). If persistence is affected, still read all four data files.

## Decide simply

Prefer the simplest design aligned with sound repository conventions. Do not introduce abstractions, CQRS, MediatR, repositories, queues, caches, events, background processing, or microservices without a concrete current problem, repository evidence, and a benefit that outweighs added ownership and failure modes. Flag material handbook/repository conflicts. Propose an ADR when changing a lasting boundary, technology, public-contract strategy, data ownership, or cross-cutting policy. Do not write implementation code.

## Produce the plan

Follow the [`FEATURE-PLAN`](../../../dotnet10-engineering-standards/templates/FEATURE-PLAN.md) structure. Include:

- outcome, current behavior, repository evidence, assumptions, and open questions;
- testable acceptance criteria, scope, and explicit non-goals;
- affected projects, boundaries, contracts, configuration, data, dependencies, and deployment surfaces;
- data and control flow, including ownership of validation, rules, transactions, side effects, and cancellation;
- actors, authentication, server-side authorization including object/tenant scope, privacy, and abuse cases;
- expected and unexpected failures, concurrency, idempotency, timeouts, retries, partial failure, recovery, and observability;
- compatibility for APIs, schemas, configuration, clients, and overlapping application versions;
- a vertical implementation sequence whose steps leave safe, reviewable states;
- rollout, migrations/backfills, flags, operational verification, abort thresholds, rollback or forward-fix, and cleanup;
- meaningful rejected alternatives;
- a risk-based test matrix for relevant actor, input, state, dependency, delivery, and output cases at the narrowest realistic levels.

Name concrete existing paths and label new paths as proposed. End with risks, blockers, and exact checks the implementer should run. Never imply an unrun check passed.
