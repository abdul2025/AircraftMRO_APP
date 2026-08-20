---
name: review-dotnet-feature
description: Review C# and .NET 10 feature changes, pull requests, branches, commits, or working-tree diffs for concrete correctness, security, data-integrity, compatibility, reliability, operability, performance, architecture, and test defects. Use for ASP.NET Core MVC or APIs, EF Core, background jobs, integrations, domain/application logic, configuration, migrations, and deployment changes. Review only unless fixes are explicitly requested.
---

# Review .NET Feature

Find evidence-backed defects that can affect users, data, security, maintainers, or operations. Prioritize behavior and risk over style. Do not edit files unless the user separately authorizes fixes.

## Resolve the standards

Locate the repository root first. The standards directory MUST exist at:

`<repository-root>/dotnet10-engineering-standards`

This skill is expected at `.agents/skills/review-dotnet-feature/SKILL.md`, so its standards links use `../../../dotnet10-engineering-standards/`.

If the standards directory or a required reference is missing, stop and report the exact missing path.

## Establish review scope

1. Identify the intended behavior and acceptance criteria from the request, plan, issue, tests, and repository evidence.
2. Identify the comparison range or diff. Include modified, added, deleted, renamed, generated, migration, configuration, test, and deployment files.
3. Inspect enough surrounding code to understand callers, dependency injection, authorization, data flow, contracts, provider behavior, runtime configuration, and existing conventions.
4. Read `CORE.md`, the review reference, and only the topic references required by changed behavior.
5. Run safe, non-mutating focused builds/tests/static checks when they materially increase confidence.

## Mandatory references

Read these files completely for every review:

- [Engineering core](../../../dotnet10-engineering-standards/CORE.md)
- [Review and static analysis](../../../dotnet10-engineering-standards/05-quality/03-review-static-analysis.md)

## Changed-topic routing

Read the applicable references before reporting findings:

- Architecture or dependency direction: [Clean Architecture](../../../dotnet10-engineering-standards/01-architecture/01-clean-architecture.md). Add [domain/application design](../../../dotnet10-engineering-standards/01-architecture/02-domain-application-design.md) or [feature design](../../../dotnet10-engineering-standards/01-architecture/03-feature-design.md) only when those specific boundaries changed.
- C# concern: select only [coding](../../../dotnet10-engineering-standards/02-csharp/01-csharp14-coding-standard.md), [async/concurrency](../../../dotnet10-engineering-standards/02-csharp/02-async-concurrency-cancellation.md), [errors/nullability](../../../dotnet10-engineering-standards/02-csharp/03-errors-results-nullability.md), or [performance/resources](../../../dotnet10-engineering-standards/02-csharp/04-performance-resource-management.md) as applicable.
- MVC/Razor behavior: [MVC/Razor](../../../dotnet10-engineering-standards/03-web/02-mvc-razor-ui.md). Add [pipeline](../../../dotnet10-engineering-standards/03-web/01-hosting-di-request-pipeline.md), [validation](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), [authentication/authorization](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md), or [web security](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md) only when that concern changed.
- API/public contract: [API contracts](../../../dotnet10-engineering-standards/03-web/03-web-api-http-contracts.md). Add [validation](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), [authentication/authorization](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md), [web security](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md), or [compatibility](../../../dotnet10-engineering-standards/03-web/07-openapi-versioning-compatibility.md) only when that concern changed.
- EF Core: select only [modeling](../../../dotnet10-engineering-standards/04-data/01-ef-core-modeling.md), [querying](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md), [transactions/concurrency](../../../dotnet10-engineering-standards/04-data/03-transactions-concurrency.md), or [migrations/lifecycle](../../../dotnet10-engineering-standards/04-data/04-migrations-data-lifecycle.md) as applicable.
- Integrations: [Integration resilience](../../../dotnet10-engineering-standards/03-web/08-integrations-resilience.md).
- Jobs, events, queues, hosted services, or SignalR: [Distributed boundaries](../../../dotnet10-engineering-standards/01-architecture/04-distributed-boundaries-messaging.md) and [Background jobs/realtime](../../../dotnet10-engineering-standards/03-web/09-background-jobs-realtime.md).
- Test changes or missing coverage: [Testing strategy](../../../dotnet10-engineering-standards/05-quality/01-testing-strategy.md); add [test implementation](../../../dotnet10-engineering-standards/05-quality/02-test-implementation.md) or [performance/reliability testing](../../../dotnet10-engineering-standards/05-quality/04-performance-reliability-testing.md) only when that concern is under review.
- Operations: select only [configuration/secrets](../../../dotnet10-engineering-standards/06-operations/01-configuration-secrets-environments.md), [observability/health](../../../dotnet10-engineering-standards/06-operations/02-observability-health.md), [CI/CD](../../../dotnet10-engineering-standards/06-operations/03-ci-cd-supply-chain.md), [deployment/rollback](../../../dotnet10-engineering-standards/06-operations/04-deployment-release-rollback.md), or [containers/hosting](../../../dotnet10-engineering-standards/06-operations/05-containers-hosting-production-readiness.md) as applicable.
- Medium/high-risk or cross-cutting diff: [Governance and quality gates](../../../dotnet10-engineering-standards/00-foundation/02-governance-quality-gates.md).
- Defect, schema, integration, authentication, contract, performance, upgrade, or hotfix diff: the applicable section of [Change playbooks](../../../dotnet10-engineering-standards/07-workflows/02-change-playbooks.md).

## Review order

1. Correctness and completeness against acceptance criteria
2. Authentication, authorization, tenant/ownership scope, input handling, secrets, and sensitive data
3. Data integrity, migrations, transaction boundaries, concurrency, idempotency, and existing-data behavior
4. Public contracts, serialization, schema, and backward compatibility
5. Failure handling, cancellation, timeouts, retries, dependency injection lifetime, disposal, and shutdown
6. Test validity and missing risk coverage
7. Logging, metrics, diagnostics, configuration, deployment, rollback, and operational ownership
8. Performance risks supported by a credible workload or query/resource path
9. Maintainability and architecture fit

## Finding threshold

Report a finding only when all are present:

- a concrete failure or material engineering risk;
- evidence in changed code and relevant context;
- a realistic trigger or execution path; and
- focused remediation direction.

Do not report subjective style preferences, speculative concerns without a path to failure, or deliberate sound repository conventions merely because the handbook offers another option.

Classify severity:

- **Critical:** likely security breach, data loss/corruption, or severe outage.
- **High:** incorrect core behavior, authorization failure, breaking contract, unsafe migration, or serious production regression.
- **Medium:** meaningful edge-case defect, reliability/operability gap, or credible failure under realistic conditions.
- **Low:** limited-impact concrete defect worth correcting; exclude cosmetic preferences.

## Required output

List findings first, ordered by severity. For each finding provide severity/title, exact file and smallest useful line range, why it fails, trigger/example, impact, and focused remediation.

Then provide open questions, test gaps, and a short review summary. If no findings meet the threshold, state that plainly and identify remaining verification limits. Never invent findings to populate the report, and never imply an unrun check passed.
