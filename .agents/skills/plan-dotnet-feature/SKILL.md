---
name: plan-dotnet-feature
description: Plan repository-grounded C# and .NET 10 features without changing production code. Use before implementing ASP.NET Core MVC or API behavior, EF Core persistence, background jobs, integrations, refactors, or cross-cutting changes when scope, architecture, contracts, data flow, risks, rollout, and verification must be decided.
---

# Plan .NET Feature

Produce an implementation-ready plan from repository evidence. Do not edit production code, tests, configuration, migrations, or deployment files while using this skill. Read-only inspection and diagnostic commands are allowed.

## Resolve the standards

Locate the repository root first. The standards directory MUST exist at:

`<repository-root>/dotnet10-engineering-standards`

This skill is expected at `.agents/skills/plan-dotnet-feature/SKILL.md`, so its standards links use `../../../dotnet10-engineering-standards/`.

If the standards directory or a required reference is missing, stop and report the exact missing path. Do not silently replace the handbook with generic guidance.

## Operating sequence

1. Inspect repository instructions, worktree state, solution and project files, target framework, package and analyzer configuration, relevant ADRs, one comparable end-to-end feature, affected tests, and deployment assumptions.
2. Trace the current path from request/event through presentation, application, domain, persistence/integration, response, and tests.
3. Frame the actor, desired outcome, current behavior, expected behavior, acceptance criteria, scope, non-goals, constraints, assumptions, and unresolved decisions.
4. Classify blast radius and risks: correctness, authorization, privacy, data integrity, compatibility, concurrency, failure recovery, performance, and operations.
5. Read `CORE.md`, the planning reference, and only the conditional references required by the affected boundaries.
6. Select the simplest complete design that fits sound repository conventions. Record material tradeoffs and rejected alternatives.
7. Produce a vertical, ordered implementation plan in which every step has an observable result and verification.

## Mandatory references

Read these files completely for every plan:

- [Engineering core](../../../dotnet10-engineering-standards/CORE.md)
- [Feature design](../../../dotnet10-engineering-standards/01-architecture/03-feature-design.md)

## Conditional reference routing

Read a reference only when its topic is materially affected. Load multiple references only when the feature genuinely crosses those boundaries:

- Domain rules, aggregates, invariants, state transitions, or use cases: [Domain and application design](../../../dotnet10-engineering-standards/01-architecture/02-domain-application-design.md).
- Module boundaries, events, queues, distributed workflows, scheduling, or multi-tenancy: [Distributed boundaries and messaging](../../../dotnet10-engineering-standards/01-architecture/04-distributed-boundaries-messaging.md).
- MVC/Razor behavior: [MVC and Razor UI](../../../dotnet10-engineering-standards/03-web/02-mvc-razor-ui.md). Add [pipeline](../../../dotnet10-engineering-standards/03-web/01-hosting-di-request-pipeline.md), [validation](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), [authentication/authorization](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md), or [web security](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md) only when that concern changes.
- HTTP API contract: [API contracts](../../../dotnet10-engineering-standards/03-web/03-web-api-http-contracts.md). Add [validation](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), [authentication/authorization](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md), [web security](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md), or [compatibility](../../../dotnet10-engineering-standards/03-web/07-openapi-versioning-compatibility.md) only when that concern changes. Use [API contract template](../../../dotnet10-engineering-standards/templates/API-CONTRACT.md) only when a formal contract artifact adds value.
- EF Core: select only the affected data concern—[modeling](../../../dotnet10-engineering-standards/04-data/01-ef-core-modeling.md), [querying](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md), [transactions/concurrency](../../../dotnet10-engineering-standards/04-data/03-transactions-concurrency.md), or [migrations/lifecycle](../../../dotnet10-engineering-standards/04-data/04-migrations-data-lifecycle.md).
- External integrations: [Integrations and resilience](../../../dotnet10-engineering-standards/03-web/08-integrations-resilience.md), plus distributed boundaries when messaging is involved.
- Background jobs or real-time delivery: [Background jobs and realtime](../../../dotnet10-engineering-standards/03-web/09-background-jobs-realtime.md), plus distributed boundaries.
- Authentication or authorization: [Authentication and authorization](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md). Add [Web security](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md) for sensitive data, uploads, or abuse risk; use the [Threat model template](../../../dotnet10-engineering-standards/templates/THREAT-MODEL.md) only when a written threat model is warranted.
- Configuration, secrets, feature flags, or certificates: [Configuration, secrets, and environments](../../../dotnet10-engineering-standards/06-operations/01-configuration-secrets-environments.md).
- Telemetry or health impact: [Observability and health](../../../dotnet10-engineering-standards/06-operations/02-observability-health.md).
- Deployment, migration ordering, rollout, or recovery impact: [Deployment and rollback](../../../dotnet10-engineering-standards/06-operations/04-deployment-release-rollback.md).
- Testing design: [Testing strategy](../../../dotnet10-engineering-standards/05-quality/01-testing-strategy.md); add [performance and reliability testing](../../../dotnet10-engineering-standards/05-quality/04-performance-reliability-testing.md) only when non-functional risk exists.
- Medium/high-risk or cross-cutting change: [Governance and quality gates](../../../dotnet10-engineering-standards/00-foundation/02-governance-quality-gates.md).
- Defect, schema, integration, authentication, contract, performance, upgrade, or hotfix plan: the applicable section of [Change playbooks](../../../dotnet10-engineering-standards/07-workflows/02-change-playbooks.md).
- Formal plan requested: [Feature plan template](../../../dotnet10-engineering-standards/templates/FEATURE-PLAN.md).

## Design rules

- Preserve a sound local pattern; propose restructuring only for a concrete correctness, security, data-integrity, compatibility, or maintainability problem.
- Keep business policy in the domain/application boundary and transport/infrastructure concerns at their edges.
- Use purpose-built DTOs and ViewModels; do not bind or expose persistence entities.
- Define authorization, validation, transaction ownership, concurrency, idempotency, cancellation, failure mapping, observability, compatibility, rollout, and recovery where applicable.
- Do not add CQRS, MediatR, repository wrappers, events, queues, caches, service splits, or new abstractions without a stated problem and net benefit.
- Require an ADR when changing a lasting system boundary, technology choice, public-contract strategy, data ownership, or cross-cutting policy. Use the [ADR template](../../../dotnet10-engineering-standards/templates/ADR.md).
- Separate confirmed repository facts, reasoned inferences, assumptions, and open questions. Ask only about choices that materially change the design.

## Required output

Return:

1. Outcome, current behavior, and acceptance criteria
2. Scope, non-goals, assumptions, and risk classification
3. Repository evidence with exact file and symbol locations
4. Recommended design, boundaries, and data/control flow
5. Contract, authorization, data, failure, compatibility, and operational decisions
6. Ordered file-by-file implementation sequence
7. Risk-based test matrix
8. Deployment, migration, rollout, and recovery impact
9. Rejected alternatives and unresolved decisions

Do not claim a build, test, migration, scan, or runtime check passed unless it was actually run successfully.
