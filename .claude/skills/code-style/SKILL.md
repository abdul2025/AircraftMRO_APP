---
name: code-style
description: The user's house style for any ASP.NET Core (.NET 10) backend — Clean Architecture solution layout (Domain / Application / Infrastructure / MVC Web and/or REST Api hosts / Tests), feature folders, Result-based error flow, EF Core + SQL Server persistence with auditing, soft delete and optimistic concurrency, and the full request path from .cshtml or HTTP endpoint down to the database query. Domain-agnostic. Use when creating a new .NET system, adding a feature or entity to one, reviewing code for conformance, or when the user says "follow my code style", "how do we structure X", or names /code-style.
---

# Code style — the .NET backend dictionary

This skill is **domain-agnostic**. Nothing in it assumes an industry. All names are
placeholders:

| Placeholder | Meaning | Example shape |
| --- | --- | --- |
| `{App}` | solution / root namespace prefix | `Contoso.Billing` |
| `{Feature}` | feature folder, **plural** PascalCase | `Invoices` |
| `{Entity}` | aggregate root, **singular** PascalCase | `Invoice` |
| `{entity}` | camelCase variable | `invoice` |
| `{feature-route}` | kebab-case API route | `invoices` |
| `{Parent}` / `{Child}` | two related entities | parent owns or is affected by children |

Never copy domain words from examples into a new system; derive names from the user's own domain.

References (load the one you need):

- [references/shared-kernel.md](references/shared-kernel.md) — base code every system starts with (entities, Result, paging, DbContext, interceptor, base controllers, JS).
- [references/feature-templates.md](references/feature-templates.md) — one complete CRUD vertical slice, every layer, with tests.
- [references/patterns.md](references/patterns.md) — recurring business patterns: aggregates with child rows, parent state derived from children, state-machine transitions, human-readable numbers, money, business time zone, statistics, sub-resource APIs.
- [references/web-ui.md](references/web-ui.md) — Razor MVC views, modal forms, list pages, live refresh, accessibility.
- [references/new-system.md](references/new-system.md) — scaffolding commands, project references, Program.cs, config, first migration.

## 1. Solution layout

```
{App}.slnx                       global.json (sdk 10.0.x, rollForward latestPatch)
src/
  {App}.Domain/                  no project or package dependencies
    Common/Entities/             BaseEntity, AuditableEntity, IHasDisplayName
    Common/Results/              Result, Result<T>
    Entities/                    {Entity}.cs  (one file per aggregate root; child entities beside it)
    Enums/{Feature}/             {Entity}Status.cs, …
  {App}.Application/             -> Domain
    Common/Interfaces/           ICurrentUser, …
    Common/Paging/               PagedRequest, PagedResponse<T>
    Features/{Feature}/
      {Entity}Service.cs         use-case orchestration
      {Entity}Errors.cs          error codes + user-facing messages
      {Feature}Options.cs        optional config section
      DTOs/                      {Entity}Dto, {Entity}ListItemDto, Create{Entity}Dto, Update{Entity}Dto,
                                 {Entity}ListFilter, {Entity}StatisticsDto, {Other}OptionDto
      Interfaces/I{Entity}Service.cs    called by the hosts
      Ports/I{Entity}Repository.cs      implemented by Infrastructure
    DependencyInjection.cs       AddApplication()
  {App}.Infrastructure/          -> Application, Domain
    Auditing/                    AuditableEntityInterceptor, CurrentUser
    Persistence/
      {App}DbContext.cs
      Features/{Feature}/        {Entity}Configuration.cs, {Entity}Repository.cs, {Entity}Queries.cs
      Migrations/
    DependencyInjection.cs       AddInfrastructure(configuration)
  {App}.Web/                     optional MVC host -> Application, Infrastructure
    Configuration/               FeatureViewLocationExpander
    Controllers/                 HomeController, ModalFormController (base)
    Features/{Feature}/
      {Feature}Controller.cs
      Models/                    {Entity}FormViewModel, {Entity}DetailsViewModel,
                                 {Entity}ListViewModel, {Entity}Labels
      Views/                     Index, Details, Create, Edit, Delete + _CreateForm, _EditForm,
                                 _DeleteForm, _{Entity}Form, _{Entity}Summary, _{Entity}StatusBadge,
                                 _{Entity}Actions, _ViewImports
    Features/_ViewImports.cshtml, Features/_ViewStart.cshtml
    Models/                      cross-feature view models and display helpers
    Views/Shared/                _Layout, _StatusMessages, shared partials
    wwwroot/js/                  modal-forms.js, other progressive enhancements
  {App}.Api/                     optional REST host -> Application, Infrastructure
    Features/{Feature}/
      {Feature}Controller.cs     [Route("api/{feature-route}")]
      Contracts/{Entity}Requests.cs, {Entity}Responses.cs
    OpenApi/                     ConcurrencyHeaders, ETag attributes, OpenAPI + Scalar setup
tests/{App}.Tests/
  Domain/  Application/  Web/  Integration/  Support/
```

Rules of the tree:
- Dependencies point inward only: hosts → Application → Domain; Infrastructure → Application.
- A host never touches the DbContext or an entity; it calls `I{Entity}Service` and gets DTOs.
- Each feature is a vertical slice: the same `{Feature}` folder name appears in every project.
- Build only the hosts the system needs (Web, Api, or both). Both share one Application layer.

## 2. Request flow (view or endpoint → database → back)

### Write (create / update / delete)

```
View: <a asp-action="Create" data-modal>   or   HTTP POST api/{feature-route}
  │
  ▼
Controller
  Web: ModalFormController — ModelState (DataAnnotations reuse domain consts) → map form VM → Create{Entity}Dto
  Api: ControllerBase — [ApiController] validates request record → map → Create{Entity}Dto
  │
  ▼
I{Entity}Service.CreateAsync(dto, ct) → Result<Guid>
  1. load what the rule needs through the port (tracked entity for writes, lookups, counts)
  2. ask the domain:  {Entity}.Create(...) / entity.Update(...) → Result   (all invariants live here)
  3. checks that need the database (uniqueness, related records, derived parent state)
  │
  ▼
I{Entity}Repository.AddAsync / UpdateAsync(entity, expectedRowVersion, ct) → Result
  sets RowVersion OriginalValue → SaveChangesAsync
  DbUpdateConcurrencyException → {Entity}.ConcurrencyConflict
  SqlException 2601/2627 on a named index → {Entity}.Duplicate…
  │
  ▼
AuditableEntityInterceptor (SaveChanges): Created/Updated/Deleted At+By, Remove → soft delete
  │
  ▼
SQL Server: rowversion check, global IsDeleted filter, filtered unique indexes

Back up
  failure → Web: AddError(code → field) + FormView(..., 422 | 409)   Api: ProblemDetails + extensions.code
  success → Web: Saved(message, url) → JSON {redirectUrl} (modal) or 302 → TempData message
            Api: 201 CreatedAtAction + ETag  |  204 NoContent
```

### Read (list / details)

```
Controller → service.ListAsync(new {Entity}ListFilter(...), new PagedRequest(page), ct)
          → repository: AsNoTracking → filters → CountAsync → OrderBy…ThenBy(Id) → Skip/Take
                        → Select(new {Entity}ListItemDto(...))      (projection in SQL, no entity load)
          → Web: new {Entity}ListViewModel(page, stats, filter) → Index.cshtml
            Api: {Entity}PageResponse built from DTOs
```

Details: repository returns `{Entity}Dto?` → service turns `null` into `{Entity}.NotFound` →
Web `NotFound()` / Api 404 problem.

### Concurrency token path

`RowVersion` shadow property → `byte[] RowVersion` on `{Entity}Dto` and `Update{Entity}Dto` →
Web: base64 hidden field, decoded by `TryDecodeRowVersion` · Api: `ETag` on reads, `If-Match` on writes
(missing → 428, stale → 412) → repository sets it as the original value before saving.

## 3. Rules per layer

**Domain**
- Aggregate root: `public sealed class {Entity} : AuditableEntity, IHasDisplayName`; private parameterless constructor; every property `{ get; private set; }`.
- Construction only through `public static Result<{Entity}> Create(...)`; changes only through intention-named methods returning `Result` (`Update`, `ChangeStatus`, `AddLine`, …). Shared validation goes in one `private Result Apply(...)` that normalizes (trim, upper-case codes, empty → null), validates, then assigns.
- Limits and error codes are `public const` on the entity (`NameMaxLength`, `ValidationErrorCode = "{Entity}.Validation"`) and are reused by EF config, form attributes and messages.
- Pure rules are `public static` predicates (`IsActiveStatus(status)`) so queries, services and view models share one definition.
- The domain never reads the clock, config or database: `now`, `today`, counts are parameters.
- Enums start at 1 (0 = "not set" is never valid) and are persisted as int.
- Business failures return `Result.Failure(code, message)`; exceptions are for bugs only.

**Application**
- `public sealed class {Entity}Service(I{Entity}Repository repository, TimeProvider timeProvider, …) : I{Entity}Service` — primary constructor DI.
- Every public method is async, returns `Result` / `Result<T>`, and takes `CancellationToken cancellationToken` last.
- `{Entity}Errors`: string codes `"{Entity}.{Reason}"` and matching `…Message` constants; domain codes re-exported.
- Writes: `GetForUpdateAsync` (tracked) → domain method → database checks → repository save. Reads: repository DTO → `Success` or `NotFound`.
- DTOs are positional `sealed record`s. Filters are records that normalize untrusted input in the constructor and expose `IsEmpty`.
- Ports (repository interfaces) speak in entities for writes and DTOs for reads; no `IQueryable` crosses the boundary.
- Name collisions between a feature namespace and an entity: `using {Entity}Entity = {App}.Domain.Entities.{Entity};`.

**Infrastructure**
- Repositories and configurations are `internal sealed`; tests see them via `InternalsVisibleTo`.
- One `IEntityTypeConfiguration<{Entity}>` per entity: table name, lengths from domain consts, `HasConversion<int>()` for enums, `HasPrecision` for decimals, relationships with `DeleteBehavior.Restrict` (aggregate-owned children: `Cascade`), unique indexes with a **const name** and filter `[IsDeleted] = 0`.
- Audit columns, `RowVersion`, `Id` generation and the soft-delete filter are applied once, generically, in `DbContext` to every `AuditableEntity` — never per entity.
- Lists always end their ordering with a unique key (`ThenBy(x => x.Id)`) so paging is stable.
- Statistics use one grouped query, then shape dictionaries in memory.
- Query rules reused across methods are `IQueryable<T>` extension methods in `{Entity}Queries`.
- `SaveAsync` converts known database exceptions into `Result` failures and rethrows the rest.

**Web host (MVC)**
- `public sealed class {Feature}Controller(I{Entity}Service {entity}Service) : ModalFormController`; explicit `[HttpGet]`/`[HttpPost]`; `CancellationToken` on every action.
- 422 = validation failure, 409 = conflict / business-state rule, `NotFound()` = unknown id; centralize in `private static int StatusFor(string code)` and `private void AddError(string code, string message)`.
- Post-Redirect-Get through `Saved(message, url)`; after delete always go to Index.
- Display-only form properties are `[BindNever]` and reloaded on re-render.

**Api host**
- `[ApiController] [Route("api/{feature-route}")] public sealed class {Feature}Controller(I{Entity}Service s) : ControllerBase`.
- Request records: `[Required]` on nullable value types (`Guid?`, `int?`, enums) so a missing field is a 400, then `!.Value` when mapping.
- Response records with `static From(dto)`; DTOs are never serialized directly; `RowVersion` travels only as `ETag`.
- One `Problem(code, message)` helper maps codes → status and adds `extensions["code"]`.
- `[ProducesResponseType<T>(status)]` for every outcome; `[ReturnsETag]` / `[RequiresIfMatch]` for concurrency.

## 4. Code style

- File-scoped namespaces matching folders; one public type per file (small companion records may share).
- `sealed` by default; `internal` for anything outside the layer's contract.
- Primary constructors for DI; no field assignments for injected services.
- Nullable enabled; `null!` only for EF-populated required properties.
- Pattern matching (`is { } value`, `is A or B`, property patterns), switch expressions for mappings, collection expressions `[]`, expression-bodied one-liners.
- Braces on every `if`; early returns for failures; blank line after each guard block.
- Full descriptive lambda parameter names (`entity => entity.Name`, not `x => x.Name`), except `_` for unused.
- Inject `TimeProvider`; never `DateTime.Now`/`UtcNow`. Store instants as `DateTimeOffset` named `…AtUtc`; calendar dates as `DateOnly`.
- Formatting for display/machine strings uses `CultureInfo.InvariantCulture`.
- XML `<summary>` on types and non-obvious members explains the business rule or the *why*; inline comments explain non-obvious decisions (ordering, concurrency, SQL limitations). No comments that restate code.
- Options: `sealed class {Name}Options { public const string SectionName = "…"; … public static bool IsValid(…) }`, validated at startup so bad config stops the host.
- Secrets never in source: `ConnectionStrings__{App}` via environment or user secrets.

## 5. Checklist — adding a feature

1. **Domain**: `Entities/{Entity}.cs` (+ child entities), `Enums/{Feature}/*`.
2. **Application**: `Features/{Feature}/` — DTOs, `Interfaces/I{Entity}Service`, `Ports/I{Entity}Repository`, `{Entity}Errors`, `{Entity}Service`; register in `AddApplication`.
3. **Infrastructure**: `Persistence/Features/{Feature}/` — Configuration, Repository, Queries; `DbSet<{Entity}>` on the DbContext; register in `AddInfrastructure`.
4. **Migration**: `dotnet ef migrations add Add{Feature} --project src/{App}.Infrastructure --startup-project src/{App}.Web --output-dir Persistence/Migrations` (use the Api project as startup if there is no Web host).
5. **Web**: `Features/{Feature}/` — controller, Models, Views (+ `_ViewImports`), nav link in `_Layout`.
6. **Api**: `Features/{Feature}/` — controller + Contracts.
7. **Tests**: Domain, Application (mocked port), Integration persistence, Api, Web — see feature-templates.md.
8. `dotnet build`, `dotnet test`; document the feature's business rules in README.

Test names are behaviour sentences in snake_case: `Update_rejects_a_stale_row_version`.
