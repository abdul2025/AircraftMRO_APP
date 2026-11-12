# Recurring patterns

Domain-agnostic solutions to problems most business systems meet. Each one names where the
code goes; combine them with the CRUD slice in [feature-templates.md](feature-templates.md).

| # | Pattern | Use when |
| --- | --- | --- |
| 1 | Aggregate with child rows | a record owns a list of lines/items/entries edited through it |
| 2 | Parent state derived from children | another record's status depends on its related records |
| 3 | State machine / workflow transitions | status moves through defined steps; some are final |
| 4 | Human-readable sequential numbers | users need `PREFIX-000123` identifiers |
| 5 | Money and precise decimals | prices, amounts, quantities with fixed scale |
| 6 | Business time zone / "today" | due dates, daily totals, "overdue" |
| 7 | Lookup / option lists | a form selects another aggregate |
| 8 | Statistics and facets | list pages with counts per status |
| 9 | Change log and live updates (optional) | audit trail, real-time UI refresh |
| 10 | Feature options | configurable behaviour per environment |

---

## 1. Aggregate with child rows

The child has no life of its own: it is created, changed and removed **only through the root**.

**Domain**

```csharp
public sealed class {Parent} : AuditableEntity, IHasDisplayName
{
    public const string LockedErrorCode = "{Parent}.Locked";
    public const int MaxChildren = 200;

    private readonly List<{Child}> _children = [];

    public IReadOnlyList<{Child}> Children => _children;

    public decimal Total => _children.Sum(child => child.LineTotal);   // derived, never stored

    public Result AddChild(Guid referenceId, string referenceName, decimal unitAmount, int quantity)
    {
        if (!CanEditChildren)
        {
            return Result.Failure(LockedErrorCode, "This record can no longer be changed.");
        }

        var created = {Child}.Create(referenceId, referenceName, unitAmount, quantity);
        if (created.IsFailure)
        {
            return Result.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        _children.Add(created.Value!);
        return Result.Success();
    }

    public Result ChangeChildQuantity(Guid childId, int quantity) { … }   // find, guard, delegate to child
    public Result RemoveChild(Guid childId) { … }
}

/// <summary>Part of the {Parent} aggregate; created and changed only through it.</summary>
public sealed class {Child} : BaseEntity
{
    private {Child}()
    {
    }

    public Guid ReferenceId { get; private set; }
    public string ReferenceName { get; private set; } = null!;     // snapshot, see pattern 5
    public decimal UnitAmount { get; private set; }
    public int Quantity { get; private set; }

    public decimal LineTotal => UnitAmount * Quantity;

    internal static Result<{Child}> Create(Guid referenceId, string referenceName, decimal unitAmount, int quantity) { … }
}
```

- Child derives from `BaseEntity` (not `AuditableEntity`): the root carries audit, soft delete and the row version.
- Child factory and mutators are `internal` so only the root can call them.

**Infrastructure**

```csharp
builder.HasMany({parent} => {parent}.Children)
    .WithOne()
    .HasForeignKey("{Parent}Id")
    .OnDelete(DeleteBehavior.Cascade);                 // owned rows go with the root
builder.Navigation({parent} => {parent}.Children).UsePropertyAccessMode(PropertyAccessMode.Field);
// {Child}Configuration: ToTable("{Children}"), HasKey, precision, FK to referenced aggregate with Restrict.
```

```csharp
public Task<{Parent}?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
    dbContext.{Parents}
        .Include({parent} => {parent}.Children)
        .SingleOrDefaultAsync({parent} => {parent}.Id == id, cancellationToken);

public Task<Result> UpdateAsync({Parent} {parent}, byte[] expectedRowVersion, CancellationToken cancellationToken)
{
    SetExpectedRowVersion({parent}, expectedRowVersion);
    // Adding or removing a child does not modify the root's own columns, so EF would not
    // check (or bump) its row version. Mark the root modified so every child change is
    // concurrency-checked against the version the caller read.
    dbContext.Entry({parent}).State = EntityState.Modified;
    return SaveAsync(cancellationToken);
}
```

Details reads project children into `IReadOnlyList<{Child}Dto>` inside the same `Select`.

**Hosts**
- Web: child actions on the parent controller (`AddChild`, `EditChild`, `RemoveChild`) using `FormView` with `_AddChildForm` partials; child list partial `_{Parent}Children` on Details.
- Api: sub-resources under the root, all guarded by the root's ETag:
  ```
  POST   api/{parents}/{id}/{children}              If-Match → 204 + new ETag
  PUT    api/{parents}/{id}/{children}/{childId}    If-Match
  DELETE api/{parents}/{id}/{children}/{childId}    If-Match
  ```
  Return the root's new `ETag` after each child write (re-read the row version).

---

## 2. Parent state derived from children

Another aggregate's status is a **function of its children** (e.g. "busy while any active
child exists, free when none"). Recompute it on **every** child write, in the **same save**.

**Domain** — the rule lives on the parent and takes counts, not collections:

```csharp
/// <summary>Sets the status from the active children after one of them changed.</summary>
public Result ApplyActiveChildren(int activeCount, bool anyUrgent)
{
    if (activeCount == 0)
    {
        if (Status is {Parent}Status.Busy or {Parent}Status.Blocked)
        {
            Status = {Parent}Status.Available;
        }

        return Result.Success();
    }

    if (Status == {Parent}Status.Disabled)
    {
        return Result.Failure(DisabledErrorCode, "A disabled record cannot have active items.");
    }

    Status = anyUrgent ? {Parent}Status.Blocked : {Parent}Status.Busy;
    return Result.Success();
}
```

**Service** — fixed order: load child → apply child change → load parent → count the *other*
active children (exclude this one) → apply → save both:

```csharp
var parent = await repository.GetParentForUpdateAsync(child.ParentId, cancellationToken);
// Counted after the parent is loaded: a sibling saved in between also writes the parent,
// so its row version rejects this save instead of it using stale counts.
var others = await repository.GetActiveChildCountsAsync(child.ParentId, excludeChildId: child.Id, cancellationToken);
var applied = parent.ApplyActiveChildren(
    others.Total + (child.IsActive ? 1 : 0),
    others.Urgent > 0 || (child.IsActive && child.IsUrgent));
if (applied.IsFailure)
{
    return applied;
}

return await repository.UpdateAsync(child, dto.RowVersion, parent, cancellationToken);
```

On create, apply the rule **before** reserving numbers or other side effects, so a rejected
create wastes nothing.

**Repository** — always write the parent so its row version participates:

```csharp
/// <summary>
/// Writes the parent even when its status did not change, so the save carries its row-version
/// check: a parent edited since it was read rejects the whole save.
/// </summary>
private void WriteParent({Parent} parent) =>
    dbContext.Entry(parent).Property(entity => entity.Status).IsModified = true;

catch (DbUpdateConcurrencyException exception)
{
    // When only the parent failed, the caller's own version is still valid, so it is retryable.
    return exception.Entries.All(entry => entry.Entity is {Parent})
        ? Result.Failure({Child}Errors.ParentChanged, {Child}Errors.ParentChangedMessage)     // 409, retry
        : Result.Failure({Child}Errors.ConcurrencyConflict, {Child}Errors.ConcurrencyConflictMessage);  // 412 / reload
}
```

Counts use a translatable query extension returning a small record:

```csharp
public sealed record ActiveChildCounts(int Total, int Urgent) { public static readonly ActiveChildCounts None = new(0, 0); }

public static async Task<ActiveChildCounts> CountActiveAsync(this IQueryable<{Child}> query, CancellationToken ct) =>
    await query.WhereActive()
        .GroupBy(_ => 1)
        .Select(group => new ActiveChildCounts(group.Count(), group.Count(child => child.IsUrgentColumn)))
        .SingleOrDefaultAsync(ct)
    ?? ActiveChildCounts.None;
```

Companion rules:
- The parent's status cannot be edited by hand while children are active (409 from the parent's update).
- A parent with active children cannot be deleted; an active child cannot be deleted (close it first, so the reason for each parent status change is recorded).

---

## 3. State machine / workflow transitions

Status changes are **actions**, not a free-form field on the edit form.

```csharp
public const string InvalidTransitionErrorCode = "{Entity}.InvalidTransition";
public const string ClosedErrorCode = "{Entity}.Closed";

private static readonly IReadOnlyDictionary<{Entity}Status, {Entity}Status[]> AllowedTransitions =
    new Dictionary<{Entity}Status, {Entity}Status[]>
    {
        [{Entity}Status.Draft] = [{Entity}Status.Submitted, {Entity}Status.Cancelled],
        [{Entity}Status.Submitted] = [{Entity}Status.Approved, {Entity}Status.Rejected, {Entity}Status.Cancelled],
        [{Entity}Status.Approved] = [{Entity}Status.Completed],
    };

/// <summary>Completed, rejected and cancelled records are final.</summary>
public static bool IsFinalStatus({Entity}Status status) =>
    status is {Entity}Status.Completed or {Entity}Status.Rejected or {Entity}Status.Cancelled;

public static bool CanMove({Entity}Status from, {Entity}Status to) =>
    AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

public Result ChangeStatus({Entity}Status next, DateTimeOffset now)
{
    if (IsFinalStatus(Status))
    {
        return Result.Failure(ClosedErrorCode, "This record is closed and can no longer change.");
    }

    if (!CanMove(Status, next))
    {
        return Result.Failure(InvalidTransitionErrorCode, $"Cannot move from {Status} to {next}.");
    }

    // per-target guards, e.g. "cannot submit without children"
    Status = next;
    CompletedAtUtc = next == {Entity}Status.Completed ? now : CompletedAtUtc;
    return Result.Success();
}
```

- Service: `ChangeStatusAsync(id, ChangeStatusDto(next, RowVersion))`.
- Web: one POST action per transition or a `_ChangeStatusForm`; the details view shows only
  buttons for `CanMove(current, target)` (labels helper exposes `NextStatuses(current)`).
- Api: `PUT api/{feature-route}/{id}/status` with `{ "status": "…" }` and `If-Match`.
- Errors `Closed` / `InvalidTransition` → 409.
- Timestamps of milestones (`SubmittedAtUtc`, `CompletedAtUtc`) are set by the domain from the `now` argument.

---

## 4. Human-readable sequential numbers

```csharp
// Configuration
public const string NumberSequenceName = "{Entity}Numbers";
public const string NumberIndexName = "IX_{Feature}_Number";
// DbContext.OnModelCreating
modelBuilder.HasSequence<long>({Entity}Configuration.NumberSequenceName);

// Repository
// Not composed with LINQ, so it runs as written: NEXT VALUE FOR is not allowed in a derived table.
private const string NextNumberSql =
    "SELECT NEXT VALUE FOR [" + {Entity}Configuration.NumberSequenceName + "] AS [Value]";

public async Task<string> NextNumberAsync(CancellationToken cancellationToken)
{
    var values = await dbContext.Database.SqlQueryRaw<long>(NextNumberSql).ToListAsync(cancellationToken);
    return $"{PREFIX}-{values.Single():D6}";
}
```

- The number is passed into `{Entity}.Create(number, …)`; `DisplayName => Number`.
- Reserve it **after** every validation that can fail cheaply; gaps from later failures are acceptable.
- Unique filtered index on `Number`; the number is never editable.

---

## 5. Money and precise decimals

- Type `decimal`; EF `HasPrecision(18, 2)` (or the scale the business needs) — declare scale as a domain const.
- Domain validates scale: `decimal.Round(amount, Scale) != amount → Invalid(...)`, and range (`>= 0`, max).
- **Snapshot** reference data copied into a record at the moment it is used (unit price, name, tax rate) so later edits to the source never change history.
- Totals are computed (`Sum` of line totals), never stored unless needed for reporting — then recompute on every child change in the same save.
- Currency: a feature option (`Currency = "USD"`), not a per-row column, unless the system is multi-currency.
- Display: a Web helper `MoneyDisplay.Format(decimal amount, string currency)` with `CultureInfo.InvariantCulture`; Api returns raw decimals.

---

## 6. Business time zone / "today"

```csharp
public sealed class {Feature}Options
{
    public const string SectionName = "{Feature}";

    /// <summary>IANA time zone whose calendar decides "today"; defaults to UTC.</summary>
    public string TimeZone { get; set; } = "UTC";

    public static bool IsValid({Feature}Options options) => TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone, out _);

    public TimeZoneInfo ResolveTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
}

// Service
private readonly TimeZoneInfo _businessTimeZone = options.ResolveTimeZone();

/// <summary>Today's date in the business time zone, so date rules follow the operator's calendar.</summary>
private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _businessTimeZone).DateTime);
```

- Pass `today` into repository reads (`ListAsync(filter, request, today, ct)`) — the database clock is never used.
- Derived flags (`IsOverdue`) are computed in the projection from `today` and a static domain predicate.

---

## 7. Lookup / option lists

```csharp
/// <summary>A {Other} a form can select.</summary>
public sealed record {Other}OptionDto(Guid Id, string DisplayName, {Other}Status Status)
{
    public bool IsSelectable => Status != {Other}Status.Disabled;
}

// Repository: capped, ordered, projected
public const int MaxOptions = 1000;
… .OrderBy(o => o.Name).Take(MaxOptions).Select(o => new {Other}OptionDto(o.Id, o.Name, o.Status)) …
```

- Lives in the **consuming** feature's DTOs (the feature that shows the dropdown).
- The form view model holds `[BindNever] IReadOnlyList<{Other}OptionDto> Options`, reloaded by the controller on every render.
- The service still validates the posted id (exists, selectable) — the dropdown is not a security boundary.

---

## 8. Statistics and facets

- One grouped query per page (`GroupBy(status[, secondKey])`), shaped into `IReadOnlyDictionary<TEnum, int>` with `CountFor(key) => GetValueOrDefault(key)`.
- `static readonly … Empty` instance for "nothing yet".
- Api responses include **every** enum key with zero, so clients need no defaults.
- Web: stat tiles and chart segments are links that set filter facets; `ListViewModel.IsFacet(...)` marks the selected one and `RouteValues(..., keepFacets: false)` builds tile links.

---

## 9. Change log and live updates (optional)

When the system needs an audit trail or real-time screens:

1. Domain `ChangeLog` entity (`Action`, `EntityType`, `EntityId`, `DisplayName`, `Message`, `ChangesJson`, `ActorUserId`, `OccurredAtUtc`).
2. In `AuditableEntityInterceptor`, **before** converting deletes into soft deletes, build one row per Added/Modified/Deleted `AuditableEntity` (skip updates that changed no business column; exclude audit/row-version columns), then `context.AddRange(rows)` — same transaction, so a failed save records nothing.
3. Web host: a `BackgroundService` polls new rows by cursor (`Id`/`OccurredAtUtc`) and pushes them through a SignalR hub; this also covers changes made through the Api host.
4. Browser: a notifications script raises `app:notification`; sections marked `data-live-refresh="{Entity}"` (optionally `data-live-refresh-id`) re-fetch the page and swap `#main-content` — deferred while a modal is open or a form has unsaved input.
5. Poll interval, batch size, replay window are options validated at startup.

---

## 10. Feature options

```csharp
public sealed class {Feature}Options
{
    public const string SectionName = "{Feature}";
    public int SomeLimit { get; set; } = 10;

    public static bool IsValid({Feature}Options options) => options.SomeLimit is > 0 and <= 1000;
}

// Program.cs (or AddInfrastructure for values the Application layer needs as a singleton)
builder.Services.AddOptions<{Feature}Options>()
    .Bind(builder.Configuration.GetSection({Feature}Options.SectionName))
    .Validate({Feature}Options.IsValid, "{Feature} settings are out of range.")
    .ValidateOnStart();
```

Invalid configuration stops the host at startup with a message naming the key.
