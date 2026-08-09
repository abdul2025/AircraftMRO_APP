---
name: review-dotnet-feature
description: Review .NET feature changes against repository evidence and the repository's normative .NET 10 engineering handbook. Use for evidence-based review of diffs, branches, commits, pull requests, migrations, tests, configuration, and deployment changes; do not fix code unless explicitly requested.
---

# Review a .NET Feature

Review only. Do not modify code, tests, configuration, migrations, deployment files, or other artifacts unless the user explicitly requests fixes.

## Establish the review

Treat [`dotnet10-engineering-standards/`](../../../dotnet10-engineering-standards/) as normative guidance. Before reviewing, verify the folder and every mandatory or selected reference exists. If anything is missing, stop and report its exact unresolved path.

Always read completely:

- [`README.md`](../../../dotnet10-engineering-standards/README.md)
- [`SKILL-USAGE-GUIDE.md`](../../../dotnet10-engineering-standards/SKILL-USAGE-GUIDE.md)
- [`00-foundation/01-engineering-principles.md`](../../../dotnet10-engineering-standards/00-foundation/01-engineering-principles.md)
- [`00-foundation/02-governance-quality-gates.md`](../../../dotnet10-engineering-standards/00-foundation/02-governance-quality-gates.md)
- [`05-quality/03-review-static-analysis.md`](../../../dotnet10-engineering-standards/05-quality/03-review-static-analysis.md)
- [`07-workflows/01-feature-workflow.md`](../../../dotnet10-engineering-standards/07-workflows/01-feature-workflow.md)
- [`07-workflows/03-master-checklists.md`](../../../dotnet10-engineering-standards/07-workflows/03-master-checklists.md)

Determine the exact review target and comparison base. Inspect the full diff for the requested working tree, branch, commit, pull request, migration, tests, configuration, and deployment changes. Read surrounding implementation, callers, contracts, project files, ADRs, relevant tests, CI/CD, and deployment assumptions; do not review isolated lines without their execution context. Preserve unrelated user work and perform no external writes.

## Route references

Read every applicable reference completely based on changed behavior; combine routes when concerns overlap:

- **Architecture/domain/application:** [`clean architecture`](../../../dotnet10-engineering-standards/01-architecture/01-clean-architecture.md), [`domain/application design`](../../../dotnet10-engineering-standards/01-architecture/02-domain-application-design.md), and [`feature design`](../../../dotnet10-engineering-standards/01-architecture/03-feature-design.md).
- **C# and runtime behavior:** [`C# standard`](../../../dotnet10-engineering-standards/02-csharp/01-csharp14-coding-standard.md), [`async/concurrency/cancellation`](../../../dotnet10-engineering-standards/02-csharp/02-async-concurrency-cancellation.md), [`errors/nullability`](../../../dotnet10-engineering-standards/02-csharp/03-errors-results-nullability.md), and [`performance/resources`](../../../dotnet10-engineering-standards/02-csharp/04-performance-resource-management.md) as applicable.
- **MVC:** [`hosting/DI/pipeline`](../../../dotnet10-engineering-standards/03-web/01-hosting-di-request-pipeline.md), [`MVC/Razor`](../../../dotnet10-engineering-standards/03-web/02-mvc-razor-ui.md), and [`validation/errors`](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md).
- **API:** [`HTTP contracts`](../../../dotnet10-engineering-standards/03-web/03-web-api-http-contracts.md), [`validation/errors`](../../../dotnet10-engineering-standards/03-web/04-validation-problem-details.md), and [`OpenAPI/versioning/compatibility`](../../../dotnet10-engineering-standards/03-web/07-openapi-versioning-compatibility.md).
- **Authentication, authorization, or security:** [`authentication/authorization`](../../../dotnet10-engineering-standards/03-web/05-authentication-authorization.md) and [`web/API security`](../../../dotnet10-engineering-standards/03-web/06-web-api-security.md).
- **Persistence, EF Core, queries, schema, or migrations:** always read all four data files: [`modeling`](../../../dotnet10-engineering-standards/04-data/01-ef-core-modeling.md), [`querying/performance`](../../../dotnet10-engineering-standards/04-data/02-querying-performance.md), [`transactions/concurrency`](../../../dotnet10-engineering-standards/04-data/03-transactions-concurrency.md), and [`migrations/lifecycle`](../../../dotnet10-engineering-standards/04-data/04-migrations-data-lifecycle.md).
- **Integrations:** [`integrations/resilience`](../../../dotnet10-engineering-standards/03-web/08-integrations-resilience.md).
- **Messaging or background/realtime work:** [`distributed boundaries/messaging`](../../../dotnet10-engineering-standards/01-architecture/04-distributed-boundaries-messaging.md) and [`background jobs/realtime`](../../../dotnet10-engineering-standards/03-web/09-background-jobs-realtime.md).
- **Testing:** [`testing strategy`](../../../dotnet10-engineering-standards/05-quality/01-testing-strategy.md), [`test implementation`](../../../dotnet10-engineering-standards/05-quality/02-test-implementation.md), and [`performance/reliability testing`](../../../dotnet10-engineering-standards/05-quality/04-performance-reliability-testing.md) when non-functional risk exists.
- **Configuration and operations:** [`configuration/secrets`](../../../dotnet10-engineering-standards/06-operations/01-configuration-secrets-environments.md) and [`observability/health`](../../../dotnet10-engineering-standards/06-operations/02-observability-health.md).
- **CI/CD, deployment, and hosting:** [`CI/CD/supply chain`](../../../dotnet10-engineering-standards/06-operations/03-ci-cd-supply-chain.md), [`deployment/release/rollback`](../../../dotnet10-engineering-standards/06-operations/04-deployment-release-rollback.md), and [`production readiness`](../../../dotnet10-engineering-standards/06-operations/05-containers-hosting-production-readiness.md).

## Review by risk

Review in this order: correctness, security, authorization, data integrity, compatibility, failure handling, concurrency, tests, observability, deployment, performance, and maintainability.

Trace changed behavior through its callers and boundaries. Check success and realistic failure paths, business invariants, authentication and resource/tenant/ownership authorization, exposed fields and over-posting, constraints and transaction ownership, concurrency/idempotency/cancellation, bounded I/O and retries, safe logs/errors, contract and mixed-version compatibility, migration rollout/recovery, test quality, operational detection, and rollback. Inspect generated migrations and configuration/deployment changes as code. Avoid subjective style feedback, speculative future concerns, unjustified abstraction demands, and issues already prevented by surrounding code or framework behavior.

Report a finding only when evidence shows a defect or material risk with a realistic trigger. Verify the relevant path and describe the observable impact. Recommend the smallest focused remediation; do not expand scope into unrelated refactoring.

Classify each finding:

- **Critical:** likely catastrophic security breach, irreversible data loss, safety failure, or release-wide outage requiring immediate stop.
- **High:** exploitable security/authorization flaw, corruption, breaking compatibility, or major production failure that should block merge.
- **Medium:** material correctness, reliability, test, observability, or deployment defect that should be fixed before merge.
- **Low:** narrow, non-cosmetic defect with limited impact and a credible trigger.

## Verify and report

Run read-only or non-mutating checks needed to validate suspected findings. Run builds or tests only when appropriate and permitted; never claim an unexecuted, failed, or unavailable check passed.

Put findings first, ordered by severity and then impact. For each finding provide:

1. severity and concise title;
2. exact file and the smallest useful line range;
3. evidence and realistic trigger;
4. resulting impact;
5. focused remediation.

Keep summaries secondary. State reviewed scope and exact checks executed with results, distinguishing pre-existing failures and limitations. If no evidence-backed defect meets the reporting threshold, state clearly: `No findings meet the reporting threshold.`
