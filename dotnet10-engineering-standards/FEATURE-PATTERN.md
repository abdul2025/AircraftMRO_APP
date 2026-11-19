# Application Feature Pattern

Use this repository-specific pattern as the default for new and materially changed
features. Preserve an equivalent existing structure when changing it would create
unrelated churn. Allow exceptions when repository conventions or concrete technical
requirements justify them.

This pattern combines Clean Architecture's outer layering (Domain / Application /
Infrastructure / presentation host) with vertical-slice organization inside Application
**and** the presentation host: each feature's use cases, DTOs, query/persistence ports,
controllers, view models, and views live together in one folder per project, rather than
being spread across technical folders. Use Clean Architecture's dependency direction to
decide what may depend on what; use the feature folder to decide where a new file for
that feature goes.

> **Naming note:** `<Feature>` is a placeholder for the feature's name throughout this
> document. File extensions, interface-prefix conventions (e.g. a leading `I`), and
> concrete framework type names are illustrative, not prescriptive — follow whatever
> convention your language, framework, and existing codebase already use. The
> architectural roles described here (service boundary, port, repository
> implementation, configuration, entity) matter more than the literal names.

> **Naming consistency:** the same `<Feature>` name is used across every project.
> `Application/Features/Users/` and `Web/Features/Users/` refer to the same feature and
> must use the same name — not `Users` in one and `UserManagement` in the other. This
> consistency is what makes the feature-folder layout useful: someone working on a
> feature opens one identically-named folder per project and finds everything about it.

## Default structure

For a feature named `<Feature>`, prefer:

```text
Application/
└── Features/
    └── <Feature>/
        ├── <Feature>Service          # use-case orchestration
        ├── DTOs/
        │   ├── Create<Feature>Dto
        │   ├── Update<Feature>Dto
        │   └── <Feature>Dto
        ├── Interfaces/
        │   └── <Feature>Service (interface / contract)
        └── Ports/
            └── <Feature>Repository (interface / contract)

Domain/
├── Common/
│   └── Results/
│       ├── Result
│       └── Result<T>
├── Entities/
│   └── <Feature>
└── Enums/
    └── <Feature>/
        └── <Feature>Status

Infrastructure/
└── Persistence/
    ├── <persistence context / unit-of-work>
    ├── Features/
    │   └── <Feature>/
    │       ├── <Feature>Configuration (mapping/schema definition)
    │       └── <Feature>Repository (implementation)
    └── Migrations/

Web/                                   # MVC presentation host — see "Presentation
└── Features/                          # (MVC host)" below for the full rationale
    └── <Feature>/
        ├── <Feature>Controller
        ├── Models/
        │   ├── Create<Feature>ViewModel
        │   └── <Feature>ViewModel
        └── Views/
            ├── Index.cshtml
            ├── Create.cshtml
            └── _<Feature>Row.cshtml
```

This is a default shape, not a requirement to create every file or folder. Use existing
repository names and organization when equivalent boundaries already exist. Do not create
duplicate folders with different spelling or casing.

`Common/Results` lives in Domain, not Application, because Domain has no dependencies of
its own: Application depends on Domain, so one canonical `Result`/`Result<T>` defined in
Domain is usable from both layers without an inward-pointing reference from Domain to
Application. See [Result handling](#result-handling).

The dependency flow through these layers is:

```text
Presentation entry point (controller / handler / route)
    ↓
Application service (the <Feature>Service contract and its implementation)
    ↓
Domain behavior and Application ports (the <Feature>Repository contract)
    ↓
Infrastructure implementation (<Feature>Repository)
```

A presentation entry point depends on the Application service only. It must never call a
repository, port, or persistence context directly, and Application must never call back
into Infrastructure except through the port it defines.

## Application feature

* Place feature use cases under `Application/Features/<Feature>/`.
* Expose use cases through one or more application services or handlers.
* Start with a single `<Feature>Service` when the feature is cohesive. Split it when use
  cases have materially different responsibilities, dependencies, or reasons to change.
* Expose the service to callers through a `<Feature>Service` contract (interface, trait,
  protocol, or equivalent construct in your language) in `Interfaces/`, implemented by
  the concrete `<Feature>Service`. This is the boundary a presentation entry point
  depends on — it is both the real seam Presentation needs (swap or mock the service in
  tests) and the reason presentation code never sees Infrastructure types.
* `Ports/<Feature>Repository` is a separate, narrower contract: the interface
  Infrastructure implements for `<Feature>Service` to call. Do not conflate the two —
  `Interfaces/` faces callers of Application, `Ports/` faces the Infrastructure
  implementation Application calls out to.
* Place feature DTOs, requests, responses, and read models inside the feature folder.
* Application services coordinate validation, authorization requiring business data,
  domain behavior, persistence, transaction intent, and mapping.
* Do not expose persistence-framework entities, contexts, lazy/deferred query objects,
  provider types, or other framework-specific results from Application.
* Register services with a dependency-injection or composition lifetime compatible with
  their dependencies (commonly a per-request/per-operation scope).
* Propagate a cancellation signal through meaningful asynchronous I/O operations, where
  the platform supports one.
* A read-only feature with no domain behavior and no write path (a simple listing or
  lookup) may skip the `Interfaces`/`Ports`/`DTOs` subfolder split and place the
  `<Feature>Repository` contract directly under `Application/<Feature>/`, implemented in
  Infrastructure — this is how `SystemFeatures` is built today. A repository contract may
  declare only the operations a feature currently needs (for example a single
  `ListVisible` operation) without implying every CRUD method exists; add the remaining
  operations to the same contract once the feature gains a write path, validation, or
  domain behavior. The same trivial-feature exception applies to the `<Feature>Service`
  contract: when `<Feature>Service` is a single-implementation pass-through with no
  branching worth mocking, the presentation entry point may depend on the concrete class
  directly instead of adding a contract purely for ceremony — add the `<Feature>Service`
  contract once the service gains real logic, a second implementation, or a test that
  needs to fake it. Don't retrofit the full ceremony onto a feature that has none of
  those yet.
* Even a read-only feature keeps a thin `<Feature>Service` in `Application/<Feature>/`
  that calls the repository and returns its DTO/read-model unchanged. The presentation
  entry point calls only the service — never the repository contract directly — so
  presentation code stays free of persistence-layer types regardless of feature size.

## Result handling

* Define one canonical `Result` and `Result<T>` in `Domain/Common/Results`, used by both
  Domain and Application. Domain has no dependency on Application, so placing the shared
  result type in Domain is what lets both layers use the same type without violating
  Clean Architecture's dependency direction.
* Domain operations return `Result`/`Result<T>` for expected business rejection. Application
  services return the same `Result`/`Result<T>` for use-case outcomes such as validation
  failure, not found, forbidden, or conflict — do not define a second Application-level
  result type.
* Use stable machine-readable error codes and safe messages. Never expose exception text,
  provider details, or secrets. Translate a failed `Result` to a structured API error
  response at the presentation boundary (see Presentation, below); do not invent a second
  error envelope.
* Infrastructure contracts return the data required by the use case. Use `Result` only
  for expected recoverable outcomes.
* Unexpected database, provider, or connectivity failures remain exceptions (or your
  language's equivalent failure signal) and are translated once at the application
  boundary.
* Put paging or validation details inside the generic value, such as
  `Result<PagedResponse<FeatureDto>>`; do not create another general result envelope.
* Specialized framework results are allowed for authentication, security, streaming,
  files, or protocol compatibility and must be translated at the appropriate boundary.

## Domain declarations

* Place domain entities and aggregates in `Domain/Entities`.
* Place business enums (or equivalent fixed-value types) under `Domain/Enums/<Feature>/`,
  grouped by the feature they belong to rather than as one flat folder.
* Domain entities own meaningful business state, invariants, calculations, and valid
  transitions.
* Domain code must not depend on a specific ORM/persistence framework, web framework,
  Infrastructure, or external providers.
* Keep table names, columns, keys, indexes, relationships, and type conversions in
  Infrastructure configuration.
* Do not treat every database table as a domain entity. Persistence-only records,
  identity tables, outbox messages, audit records, join tables, and read models may
  remain in Infrastructure when they do not represent domain concepts.
* Read-only features may use projections without introducing a domain entity when no
  domain behavior exists.

## Infrastructure persistence and repositories

* Keep the persistence context, migrations, schema/mapping configuration, repository
  implementations, messaging, files, and external integrations in Infrastructure.
* Define narrow repository contracts — a collection-like abstraction over one entity or
  aggregate, exposing only the operations a feature's use cases actually need (a
  read-only feature may declare just list/get operations; add create/update/remove
  only once a write path exists) — in the inner layer that consumes them, normally
  beside the Application feature. Infrastructure implements those contracts.
* Compose and execute persistence-framework queries only in Infrastructure. Do not
  expose lazy/deferred query objects or provider-specific types across the boundary.
* Apply filtering, sorting, projection, pagination, tenant or ownership scope, and
  cancellation before materialization. Default to a non-tracking/read-only query mode
  with a projection to the DTO/read-model shape for read paths; only load a tracked
  entity graph when the same operation updates it.
* Return materialized domain objects, application read models, or values required by the
  use case.
* Application owns use-case sequencing and transaction intent. Infrastructure owns
  transaction implementation.
* Consider concurrency and idempotency for competing or retried writes.

Persistence may be organized by technical concern or by feature. By feature is the
default shown in [Default structure](#default-structure):

```text
Infrastructure/
└── Persistence/
    └── Features/
        └── <Feature>/
            ├── <Feature>Configuration
            └── <Feature>Repository
```

Follow the repository's existing convention. The required rule is that persistence and
provider-specific implementation remain in Infrastructure, not that every codebase uses
one folder layout.

## Identity and authentication features

Authentication, registration, and credential management (password, 2FA, passkey/WebAuthn,
external login) run through the platform's built-in identity/authentication framework,
when one exists. Treat its user-manager, sign-in-manager, and credential stores as the
persistence and port layer for these use cases — do not wrap them in a second
repository-style abstraction; that duplicates a boundary the identity framework already
owns.

* An identity-only action (sign in, sign out, register, change password, add a passkey)
  may be called directly from a thin presentation entry point or a small Application
  service that composes the identity/sign-in framework's calls; either is acceptable,
  pick one convention and keep new identity features consistent with it.
* A feature that combines identity with real domain data (for example, linking a user
  to a role-specific profile, an assignment, or an audit trail) still follows the
  standard Application/Domain/Infrastructure split above for that domain-specific part;
  only the credential mechanics stay with the identity framework.
* Not every presentation host gets a given identity framework's client-side flows (e.g.
  passkey UI) for free — some ship only with specific host templates. Confirm what a
  given host provides before assuming a feature is free; building it elsewhere is
  additional, explicitly scoped work.
* When multiple hosts (e.g. a browser-facing host and an API host) share the same
  identity configuration, configure each host's authentication events independently: a
  browser host typically redirects an unauthenticated request to a sign-in page; an API
  host typically returns an unauthenticated/forbidden status code. Do not assume one
  default applies to both hosts.
* Under a feature-folder presentation host (see below), identity-only actions still get
  their own folder — e.g. `Web/Features/Auth/` or `Web/Account/` — following the same
  `Controller` + `Models/` + `Views/` shape as any other feature, even when the
  corresponding Application layer has no service backing it. Pick one location and name
  it consistently across the codebase; do not scatter identity views across both
  `Features/Auth/` and a top-level `Account/` folder.

## Presentation

* Presentation entry points (controllers, endpoints, route handlers) handle binding,
  authentication context, response mapping, and serialization.
* Presentation entry points depend only on the Application service boundary (the
  `<Feature>Service` contract, or the concrete `<Feature>Service` for the trivial
  read-only exception in [Application feature](#application-feature)) — never directly
  on a repository, port, or persistence context. This keeps the entry point light and
  keeps persistence types (entities, lazy query objects, provider-specific results) from
  leaking into presentation code even when the service is a thin pass-through.
* Keep business rules and persistence-framework queries out of presentation entry points.
* Do not bind or return persistence entities directly.
* Presentation entry points translate what the Application service returns into the
  response shape their host uses: a rendered view, a redirect, validation errors, or a
  structured API error response. Translate a failed `Result`/`Result<T>` at this
  boundary; do not leak exception text or invent a second error envelope.
* Application services return `Result`/`Result<T>` or a plain DTO — never a
  framework-specific action/response/view result type or an HTTP status code directly.
  Those types belong to the presentation entry point that owns the host, not to
  Application.
* UI-layer view models are a distinct thing from Application DTOs, not the same file
  reused across boundaries. A view model carries display/binding concerns — display
  metadata and validation attributes, option-list structures for form controls, and
  fields the view needs but the application layer does not (for example a "confirm
  password" field). Place view models under the presentation project's own models
  folder for that feature and map to/from the Application DTO in the presentation entry
  point. Do not add view-only attributes to an Application DTO to avoid writing a
  mapping.
* Require an anti-forgery/CSRF token on every state-changing action reachable by a
  cookie-authenticated browser session, using whatever mechanism your web framework
  provides for this (form-binding helpers that emit the token automatically, or an
  explicit token-validation attribute/middleware on the action) — or a single
  repository-wide anti-forgery convention, if that is the established convention. An
  API endpoint authenticated by a bearer token instead of a cookie does not need this
  control.

### Presentation (MVC host)

The MVC presentation host follows the same vertical-slice principle as Application:
each feature owns one folder containing its controller, view models, and views, rather
than the framework's technical-folder default (`Controllers/`, `Views/`, `Models/` each
as a single flat top-level folder).

```text
Web/
├── Features/
│   └── <Feature>/
│       ├── <Feature>Controller
│       ├── Models/
│       │   ├── Create<Feature>ViewModel
│       │   └── <Feature>ViewModel
│       └── Views/
│           ├── Index.cshtml
│           ├── Create.cshtml
│           └── _<Feature>Row.cshtml
├── Shared/
│   └── Views/
│       ├── _Layout.cshtml
│       ├── _ValidationScriptsPartial.cshtml
│       └── Error.cshtml
├── Filters/
├── Middleware/
└── Authorization/
    └── HasPermissionAttribute
```

* Register a view-location expander (or equivalent mechanism for your MVC framework) in
  composition/startup so the default view engine resolves
  `Features/{Feature}/Views/{Controller}/{Action}.cshtml`. Without it, views placed
  under a feature folder will not be found by the default view engine — this is the one
  extra piece of setup a feature-folder MVC layout requires beyond the framework
  default.
* `Shared/` holds only views used by two or more features (layout, validation scripts
  partial, error pages). A partial used by a single feature stays inside that feature's
  own `Views/` folder. Do not let `Shared/` accumulate feature-specific views for
  convenience.
* Filters, middleware, and cross-cutting authorization attributes are host-level
  concerns and live outside `Features/`, even under a feature-folder layout — they are
  not tied to any single feature's lifecycle.
* When an API host exists alongside the MVC host, API request/response contracts are a
  third, distinct set of types from Application DTOs and MVC view models — do not reuse
  MVC view models as API contracts or vice versa. Map each independently to/from the
  Application DTO at its own presentation boundary.

## Testing

Apply risk-based coverage:

* Unit-test domain invariants and pure application decisions.
* Integration-test persistence queries, mappings, transactions, authorization,
  serialization, and dependency-injection wiring when relevant, against the real
  database provider (see [Engineering core](CORE.md)) rather than an in-memory
  stand-in for the persistence framework.
* When no real database engine is reachable in the working environment (no running
  container, no configured instance), a real-database integration test may be deferred
  instead of written against an in-memory persistence stand-in — in-memory providers
  do not validate query translation, constraints, or concurrency and would give false
  confidence. Keep unit-test coverage of the Application/Presentation logic that
  consumes the repository (against a stub or fake implementing the port), and call out
  the deferred real-database test explicitly as a known, tracked gap rather than
  silently dropping it.
* Cover important validation, forbidden, not-found, conflict, concurrency, cancellation,
  and failure paths based on feature risk.
* Test observable behavior rather than implementation details or mock call counts.

## Feature completion

A feature is complete when its acceptance criteria are implemented, boundaries remain
clear, persistence stays in Infrastructure, expected application outcomes use the
canonical result where appropriate, dependency injection is wired, and relevant
risk-based tests pass.

Document any meaningful exception to this pattern and the concrete reason for it.