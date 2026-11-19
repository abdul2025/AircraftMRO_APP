# .NET 10 Engineering Core

Apply this baseline to planning, implementation, review, and testing. Scale the depth
of work to the feature's risk and blast radius.

## Priorities

1. Correct observable behavior
2. Security, privacy, and data integrity
3. Clear and maintainable code
4. Consistency with sound repository conventions
5. Testability and operability
6. Performance supported by evidence
7. Extensibility for known change

Make a material tradeoff explicit when these priorities conflict.

## Repository first

- Inspect repository instructions, solution and project files, configuration, nearby
  behavior, tests, and current worktree changes before deciding or editing.
- Follow the repository's supported .NET/C# version, build commands, analyzers,
  formatting, dependency policy, and established patterns unless they create a
  concrete defect.
- Preserve unrelated user work. Keep changes cohesive and avoid opportunistic
  restructuring or formatting.
- Ask only when a missing decision materially changes behavior, security, data,
  compatibility, destructive impact, or scope.

## Requirements and scope

- Define the actor, observable outcome, acceptance criteria, important failure cases,
  scope, and non-goals.
- Distinguish repository facts, assumptions, and unresolved decisions.
- Prefer the simplest design that completely satisfies current requirements.
- Do not add a layer, abstraction, library, event, queue, cache, or service split
  without a concrete problem that justifies its cost.

## Responsibilities and boundaries

- Keep business rules and invariants out of controllers, views, persistence mapping,
  migrations, and external-service adapters.
- Keep transport models separate from persistence entities. Use purpose-built request,
  response, or view models when data crosses a boundary.
- Keep technical details such as EF Core, identity, messaging, files, and external APIs
  behind the code that owns those integrations.
- Boundaries are responsibilities and dependency direction, not a required number of
  projects or classes. Organizing the Application layer by feature (a "vertical slice"
  inside each layer) is the expected default, not an exception to layering.

## Correctness, security, and data

- Validate untrusted input at the boundary and enforce authoritative business rules in
  the code that owns the use case or domain behavior.
- Enforce authentication, authorization, resource ownership, and tenant scope on the
  server where applicable.
- Harden cookie authentication explicitly: `HttpOnly = true`, `SecurePolicy =
  CookieSecurePolicy.Always`, an explicit `SameSite` mode, sliding expiration with a
  bounded lifetime, and re-validate the principal (`ValidatePrincipal`/
  `SecurityStampValidator`) so a revoked or changed account is rejected without waiting
  for cookie expiry.
- Configure authentication response handling per host: a browser-facing MVC host
  redirects an unauthenticated request to sign-in; an API host returns `401`/`403`
  without a redirect. In ASP.NET Core 10, API-shaped endpoints no longer redirect by
  default — confirm this matches intent when a Web host and an API host share the same
  Identity/cookie configuration.
- Keep secrets and unnecessary sensitive data out of source, logs, errors, telemetry,
  tests, and generated artifacts.
- Define transaction ownership for atomic work. Consider concurrency, uniqueness,
  idempotency, retries, and partial failure when the operation can race or repeat.
- Preserve public API, event, and database compatibility unless a breaking change is
  intentional, approved, and safely migrated.

## C# and runtime behavior

- Use clear names, nullable reference types, and truthful null, empty, time, money,
  enum, and unit semantics.
- Use asynchronous APIs for I/O and propagate `CancellationToken` through meaningful
  I/O paths. Avoid sync-over-async and unowned fire-and-forget work.
- Current C# language features (for example the `field` keyword, extension members, and
  span/array conversions) are welcome where they remove real boilerplate. Do not adopt a
  new language feature purely because it is new, and never let a preview language
  feature reach a production path without explicit approval.
- Match dependency-injection lifetimes to ownership; do not capture scoped or
  disposable services in longer-lived objects.
- Catch exceptions only to handle, translate, clean up, or add useful boundary context.
  Preserve stack traces and avoid duplicate exception logging.
- Dispose owned resources and bound collections, payloads, queries, parallelism,
  retries, and queues.

## Web, data, and integrations

- Keep HTTP endpoints/controllers thin: authorize, bind, invoke behavior, and map the
  result to an explicit contract.
- Keep safe HTTP methods free of state changes. Require an anti-forgery token on every
  cookie-authenticated state-changing request (form post or fetch) and encode untrusted
  browser output; do not treat one control as a substitute for the other.
- Return intentional status and error contracts. Use `ProblemDetails` (RFC 9457) as the
  default machine-readable error shape for both hosts; do not leak internal exceptions
  or persistence shapes.
- Default EF Core reads to `AsNoTracking()` with a projection to the shape the caller
  needs; only track entities that the same unit of work will update. Watch for and
  eliminate N+1 query patterns before considering lower-level optimizations.
- For EF Core behavior, verify against the production database provider when
  translation, constraints, precision, transactions, concurrency, or migrations matter.
- Review generated migrations and account for existing data, locks, mixed application
  versions, destructive operations, and recovery. Treat a schema change that risks data
  loss with the expand-contract pattern: add the new shape, deploy code that tolerates
  both, backfill, then remove the old shape in a later change.
- Give outbound calls timeouts. Retry only transient failures when repeating the
  operation is safe, and keep vendor-specific models and errors at the integration edge.

## Testing and verification

- Test observable behavior and meaningful risk, not class count, mocks, or coverage
  percentage.
- Unit-test pure decisions; integration-test framework, provider, serialization,
  authentication, authorization, dependency-injection, and infrastructure behavior.
- When a test's value depends on real EF Core/provider translation, run it against the
  actual database engine (for example a containerized SQL Server), not the EF Core
  InMemory provider — InMemory does not validate SQL translation, constraints, or
  concurrency behavior.
- Add regression coverage for fixed defects when practical.
- Run the narrowest useful checks first, then broaden according to blast radius.
- Never claim an unrun build, test, migration, scan, or runtime check passed. Report the
  exact checks, results, environmental limits, and remaining risk.

## Operations and safe delivery

- Keep environment configuration external and validate critical options. Protect
  secrets and certificates.
- Apply database migrations from the deployment pipeline as an explicit step (an
  idempotent SQL script or a migration bundle), reviewed before it runs. Do not call
  `Database.Migrate()` at application startup in production; it races multiple
  instances and hides the change from review.
- Add structured, useful telemetry for important outcomes and failures without leaking
  sensitive data or creating unbounded labels.
- Treat public contracts and database migrations as deployment compatibility events.
- For material production impact, define rollout, smoke verification, observation,
  rollback or forward-fix, and data reconciliation.
- Never mutate a live environment without explicit target authorization.

## Definition of done

Work is done when the requested outcome is complete, applicable security and data risks
are addressed, relevant checks have passed, the final change is focused, operational
impact is understood, and remaining limitations are reported honestly.
