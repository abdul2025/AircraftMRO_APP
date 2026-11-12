# Feature template — one CRUD vertical slice

Placeholders: `{App}`, `{Feature}` (plural), `{Entity}` (singular), `{entity}` (camelCase),
`{feature-route}` (kebab-case). The sample fields are deliberately generic —
`Code` (unique business key), `Name`, `Description`, `Status` — replace them with the real ones.
For children, derived state, workflows, numbering or money, combine with [patterns.md](patterns.md).

---

## Domain

`Entities/{Entity}.cs`

```csharp
using {App}.Domain.Common.Entities;
using {App}.Domain.Common.Results;
using {App}.Domain.Enums.{Feature};

namespace {App}.Domain.Entities;

/// <summary>What this entity represents and the one rule a reader must know about it.</summary>
public sealed class {Entity} : AuditableEntity, IHasDisplayName
{
    public const string ValidationErrorCode = "{Entity}.Validation";
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 2000;

    private {Entity}()
    {
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public {Entity}Status Status { get; private set; }

    public string DisplayName => Code;

    public static Result<{Entity}> Create(string code, string name, string? description, {Entity}Status status)
    {
        var {entity} = new {Entity}();
        var result = {entity}.Apply(code, name, description, status);

        return result.IsSuccess
            ? Result<{Entity}>.Success({entity})
            : Result<{Entity}>.Failure(result.ErrorCode!, result.ErrorMessage!);
    }

    public Result Update(string code, string name, string? description, {Entity}Status status) =>
        Apply(code, name, description, status);

    /// <summary>Codes compare case-insensitively, so they are stored upper-case and trimmed.</summary>
    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private Result Apply(string code, string name, string? description, {Entity}Status status)
    {
        var normalizedCode = NormalizeCode(code);
        var trimmedName = name?.Trim() ?? string.Empty;
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (normalizedCode.Length is 0 or > CodeMaxLength)
        {
            return Invalid($"Code is required and must be at most {CodeMaxLength} characters.");
        }

        if (trimmedName.Length is 0 or > NameMaxLength)
        {
            return Invalid($"Name is required and must be at most {NameMaxLength} characters.");
        }

        if (trimmedDescription is { Length: > DescriptionMaxLength })
        {
            return Invalid($"Description must be at most {DescriptionMaxLength} characters.");
        }

        if (!Enum.IsDefined(status))
        {
            return Invalid("Status is not valid.");
        }

        Code = normalizedCode;
        Name = trimmedName;
        Description = trimmedDescription;
        Status = status;

        return Result.Success();
    }

    private static Result Invalid(string message) => Result.Failure(ValidationErrorCode, message);
}
```

`Enums/{Feature}/{Entity}Status.cs`

```csharp
namespace {App}.Domain.Enums.{Feature};

public enum {Entity}Status
{
    Active = 1,
    Inactive = 2
}
```

---

## Application — `Features/{Feature}/`

`DTOs/`

```csharp
public sealed record {Entity}Dto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    {Entity}Status Status,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);

public sealed record {Entity}ListItemDto(Guid Id, string Code, string Name, {Entity}Status Status);

public sealed record Create{Entity}Dto(string Code, string Name, string? Description, {Entity}Status Status);

public sealed record Update{Entity}Dto(string Code, string Name, string? Description, {Entity}Status Status, byte[] RowVersion);

/// <summary>Optional list filters; untrusted query-string input is normalized here, once.</summary>
public sealed record {Entity}ListFilter
{
    public const int MaxSearchLength = 100;

    public static readonly {Entity}ListFilter None = new();

    public {Entity}ListFilter(string? search = null, {Entity}Status? status = null)
    {
        var trimmed = search?.Trim();
        Search = string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length > MaxSearchLength ? trimmed[..MaxSearchLength] : trimmed;
        Status = status is { } value && Enum.IsDefined(value) ? value : null;
    }

    public string? Search { get; }
    public {Entity}Status? Status { get; }

    public bool IsEmpty => Search is null && Status is null;
}

public sealed record {Entity}StatisticsDto(int TotalCount, IReadOnlyDictionary<{Entity}Status, int> CountByStatus)
{
    public int CountFor({Entity}Status status) => CountByStatus.GetValueOrDefault(status);
}
```

`{Entity}Errors.cs`

```csharp
using {Entity}Entity = {App}.Domain.Entities.{Entity};

namespace {App}.Application.Features.{Feature};

public static class {Entity}Errors
{
    public const string NotFound = "{Entity}.NotFound";
    public const string Validation = {Entity}Entity.ValidationErrorCode;
    public const string DuplicateCode = "{Entity}.DuplicateCode";
    public const string ConcurrencyConflict = "{Entity}.ConcurrencyConflict";

    public const string NotFoundMessage = "The record was not found.";
    public const string DuplicateCodeMessage = "Another record already uses this code.";
    public const string ConcurrencyConflictMessage =
        "This record was changed by someone else after you opened it. Reload and try again.";
}
```

`Interfaces/I{Entity}Service.cs`

```csharp
public interface I{Entity}Service
{
    Task<Result<PagedResponse<{Entity}ListItemDto>>> ListAsync({Entity}ListFilter filter, PagedRequest request, CancellationToken cancellationToken);

    Task<Result<{Entity}StatisticsDto>> GetStatisticsAsync(CancellationToken cancellationToken);

    Task<Result<{Entity}Dto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(Create{Entity}Dto dto, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(Guid id, Update{Entity}Dto dto, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken);
}
```

`Ports/I{Entity}Repository.cs`

```csharp
public interface I{Entity}Repository
{
    Task<PagedResponse<{Entity}ListItemDto>> ListAsync({Entity}ListFilter filter, PagedRequest request, CancellationToken cancellationToken);

    Task<{Entity}StatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken);

    Task<{Entity}Dto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a tracked entity for a write in the same operation.</summary>
    Task<{Entity}?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken);

    Task<Result> AddAsync({Entity} {entity}, CancellationToken cancellationToken);

    /// <summary>Fails with a concurrency error when the record changed since it was read.</summary>
    Task<Result> UpdateAsync({Entity} {entity}, byte[] expectedRowVersion, CancellationToken cancellationToken);

    /// <summary>Soft-deletes; fails with a concurrency error when the record changed since it was read.</summary>
    Task<Result> DeleteAsync({Entity} {entity}, byte[] expectedRowVersion, CancellationToken cancellationToken);
}
```

`{Entity}Service.cs`

```csharp
using {Entity}Entity = {App}.Domain.Entities.{Entity};

namespace {App}.Application.Features.{Feature};

public sealed class {Entity}Service(I{Entity}Repository repository) : I{Entity}Service
{
    public async Task<Result<PagedResponse<{Entity}ListItemDto>>> ListAsync(
        {Entity}ListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken) =>
        Result<PagedResponse<{Entity}ListItemDto>>.Success(await repository.ListAsync(filter, request, cancellationToken));

    public async Task<Result<{Entity}StatisticsDto>> GetStatisticsAsync(CancellationToken cancellationToken) =>
        Result<{Entity}StatisticsDto>.Success(await repository.GetStatisticsAsync(cancellationToken));

    public async Task<Result<{Entity}Dto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var {entity} = await repository.GetByIdAsync(id, cancellationToken);

        return {entity} is null
            ? Result<{Entity}Dto>.Failure({Entity}Errors.NotFound, {Entity}Errors.NotFoundMessage)
            : Result<{Entity}Dto>.Success({entity});
    }

    public async Task<Result<Guid>> CreateAsync(Create{Entity}Dto dto, CancellationToken cancellationToken)
    {
        var created = {Entity}Entity.Create(dto.Code, dto.Name, dto.Description, dto.Status);
        if (created.IsFailure)
        {
            return Result<Guid>.Failure(created.ErrorCode!, created.ErrorMessage!);
        }

        var {entity} = created.Value!;
        // Friendly early check; the filtered unique index is still the real guarantee.
        if (await repository.CodeExistsAsync({entity}.Code, excludeId: null, cancellationToken))
        {
            return Result<Guid>.Failure({Entity}Errors.DuplicateCode, {Entity}Errors.DuplicateCodeMessage);
        }

        var saved = await repository.AddAsync({entity}, cancellationToken);

        return saved.IsSuccess
            ? Result<Guid>.Success({entity}.Id)
            : Result<Guid>.Failure(saved.ErrorCode!, saved.ErrorMessage!);
    }

    public async Task<Result> UpdateAsync(Guid id, Update{Entity}Dto dto, CancellationToken cancellationToken)
    {
        var {entity} = await repository.GetForUpdateAsync(id, cancellationToken);
        if ({entity} is null)
        {
            return Result.Failure({Entity}Errors.NotFound, {Entity}Errors.NotFoundMessage);
        }

        var updated = {entity}.Update(dto.Code, dto.Name, dto.Description, dto.Status);
        if (updated.IsFailure)
        {
            return updated;
        }

        if (await repository.CodeExistsAsync({entity}.Code, excludeId: id, cancellationToken))
        {
            return Result.Failure({Entity}Errors.DuplicateCode, {Entity}Errors.DuplicateCodeMessage);
        }

        return await repository.UpdateAsync({entity}, dto.RowVersion, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var {entity} = await repository.GetForUpdateAsync(id, cancellationToken);
        if ({entity} is null)
        {
            return Result.Failure({Entity}Errors.NotFound, {Entity}Errors.NotFoundMessage);
        }

        // Business rules that forbid deletion go here, e.g. "only inactive records can be deleted".

        return await repository.DeleteAsync({entity}, rowVersion, cancellationToken);
    }
}
```

Register: `services.AddScoped<I{Entity}Service, {Entity}Service>();`

---

## Infrastructure — `Persistence/Features/{Feature}/`

`{Entity}Configuration.cs`

```csharp
internal sealed class {Entity}Configuration : IEntityTypeConfiguration<{Entity}>
{
    public const string CodeIndexName = "IX_{Feature}_Code";
    private const string NotDeletedFilter = "[IsDeleted] = 0";

    public void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        builder.ToTable("{Feature}");
        builder.HasKey({entity} => {entity}.Id);

        builder.Property({entity} => {entity}.Code).HasMaxLength({Entity}.CodeMaxLength).IsRequired();
        builder.Property({entity} => {entity}.Name).HasMaxLength({Entity}.NameMaxLength).IsRequired();
        builder.Property({entity} => {entity}.Description).HasMaxLength({Entity}.DescriptionMaxLength);
        builder.Property({entity} => {entity}.Status).HasConversion<int>();

        // Uniqueness applies to live rows only, so the code of a soft-deleted record can be reused.
        builder.HasIndex({entity} => {entity}.Code)
            .HasDatabaseName(CodeIndexName)
            .IsUnique()
            .HasFilter(NotDeletedFilter);

        // Reference to another aggregate: never cascade — rows are only soft-deleted.
        // builder.HasOne<{Other}>().WithMany().HasForeignKey(e => e.{Other}Id).IsRequired().OnDelete(DeleteBehavior.Restrict);
        // Index the columns lists filter and group by:
        // builder.HasIndex(e => new { e.{Other}Id, e.Status });
    }
}
```

DbContext: `public DbSet<{Entity}> {Feature} => Set<{Entity}>();` — audit columns, RowVersion and
the soft-delete filter come from `ConfigureAuditableEntities`.

`{Entity}Repository.cs`

```csharp
internal sealed class {Entity}Repository({App}DbContext dbContext) : I{Entity}Repository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<PagedResponse<{Entity}ListItemDto>> ListAsync(
        {Entity}ListFilter filter,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.{Feature}.AsNoTracking();

        if (filter.Status is { } status)
        {
            query = query.Where({entity} => {entity}.Status == status);
        }

        if (filter.Search is { } search)
        {
            // EF Core escapes LIKE wildcards in the term; the column collation makes it case-insensitive.
            query = query.Where({entity} => {entity}.Code.Contains(search) || {entity}.Name.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy({entity} => {entity}.Code)
            .ThenBy({entity} => {entity}.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select({entity} => new {Entity}ListItemDto({entity}.Id, {entity}.Code, {entity}.Name, {entity}.Status))
            .ToListAsync(cancellationToken);

        return new PagedResponse<{Entity}ListItemDto>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<{Entity}StatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        var groups = await dbContext.{Feature}
            .AsNoTracking()
            .GroupBy({entity} => {entity}.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return new {Entity}StatisticsDto(
            groups.Sum(group => group.Count),
            groups.ToDictionary(group => group.Status, group => group.Count));
    }

    public Task<{Entity}Dto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.{Feature}
            .AsNoTracking()
            .Where({entity} => {entity}.Id == id)
            .Select({entity} => new {Entity}Dto(
                {entity}.Id,
                {entity}.Code,
                {entity}.Name,
                {entity}.Description,
                {entity}.Status,
                {entity}.CreatedAtUtc,
                {entity}.CreatedBy,
                {entity}.UpdatedAtUtc,
                {entity}.UpdatedBy,
                EF.Property<byte[]>({entity}, {App}DbContext.RowVersionPropertyName)))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<{Entity}?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.{Feature}.SingleOrDefaultAsync({entity} => {entity}.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken) =>
        dbContext.{Feature}
            .AsNoTracking()
            .AnyAsync({entity} => {entity}.Code == code && (excludeId == null || {entity}.Id != excludeId), cancellationToken);

    public Task<Result> AddAsync({Entity} {entity}, CancellationToken cancellationToken)
    {
        dbContext.{Feature}.Add({entity});
        return SaveAsync(cancellationToken);
    }

    public Task<Result> UpdateAsync({Entity} {entity}, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        SetExpectedRowVersion({entity}, expectedRowVersion);
        return SaveAsync(cancellationToken);
    }

    public Task<Result> DeleteAsync({Entity} {entity}, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        SetExpectedRowVersion({entity}, expectedRowVersion);
        dbContext.{Feature}.Remove({entity});   // the interceptor turns this into a soft delete
        return SaveAsync(cancellationToken);
    }

    private void SetExpectedRowVersion({Entity} {entity}, byte[] expectedRowVersion) =>
        dbContext.Entry({entity}).Property({App}DbContext.RowVersionPropertyName).OriginalValue = expectedRowVersion;

    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure({Entity}Errors.ConcurrencyConflict, {Entity}Errors.ConcurrencyConflictMessage);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql
            && sql.Message.Contains({Entity}Configuration.CodeIndexName, StringComparison.Ordinal))
        {
            return Result.Failure({Entity}Errors.DuplicateCode, {Entity}Errors.DuplicateCodeMessage);
        }
    }
}
```

Register: `services.AddScoped<I{Entity}Repository, {Entity}Repository>();`

---

## Web host — `Features/{Feature}/`

`{Feature}Controller.cs`

```csharp
public sealed class {Feature}Controller(I{Entity}Service {entity}Service) : ModalFormController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search = null,
        {Entity}Status? status = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = new {Entity}ListFilter(search, status);
        var list = await {entity}Service.ListAsync(filter, new PagedRequest(page), cancellationToken);
        var statistics = await {entity}Service.GetStatisticsAsync(cancellationToken);

        return View(new {Entity}ListViewModel(list.Value!, statistics.Value!, filter));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await {entity}Service.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? View({Entity}DetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpGet]
    public IActionResult Create() => FormView(nameof(Create), new {Entity}FormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create({Entity}FormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return FormView(nameof(Create), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await {entity}Service.CreateAsync(
            new Create{Entity}Dto(form.Code, form.Name, form.Description, form.Status!.Value),
            cancellationToken);

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return FormView(nameof(Create), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Record created.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id = result.Value })!);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await {entity}Service.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var {entity} = result.Value!;
        return FormView(nameof(Edit), new {Entity}FormViewModel
        {
            Code = {entity}.Code,
            Name = {entity}.Name,
            Description = {entity}.Description,
            Status = {entity}.Status,
            RowVersion = Convert.ToBase64String({entity}.RowVersion)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, {Entity}FormViewModel form, CancellationToken cancellationToken)
    {
        if (!TryDecodeRowVersion(form.RowVersion, out var rowVersion))
        {
            ModelState.AddModelError(string.Empty, {Entity}Errors.ConcurrencyConflictMessage);
        }

        if (!ModelState.IsValid)
        {
            return FormView(nameof(Edit), form, StatusCodes.Status422UnprocessableEntity);
        }

        var result = await {entity}Service.UpdateAsync(
            id,
            new Update{Entity}Dto(form.Code, form.Name, form.Description, form.Status!.Value, rowVersion),
            cancellationToken);

        if (result.ErrorCode == {Entity}Errors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            AddError(result.ErrorCode!, result.ErrorMessage!);
            return FormView(nameof(Edit), form, StatusFor(result.ErrorCode!));
        }

        return Saved("Record updated.", ModalReturnUrl ?? Url.Action(nameof(Details), new { id })!);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await {entity}Service.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? FormView(nameof(Delete), {Entity}DetailsViewModel.From(result.Value!)) : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        Result result = TryDecodeRowVersion(rowVersion, out var expectedRowVersion)
            ? await {entity}Service.DeleteAsync(id, expectedRowVersion, cancellationToken)
            : Result.Failure({Entity}Errors.ConcurrencyConflict, {Entity}Errors.ConcurrencyConflictMessage);

        if (result.ErrorCode == {Entity}Errors.NotFound)
        {
            return NotFound();
        }

        if (result.IsFailure)
        {
            TempData[ErrorMessageKey] = result.ErrorMessage;
            if (!IsModalRequest)
            {
                return RedirectToAction(nameof(Delete), new { id });
            }

            // Re-render the confirmation with the current values and row version.
            var current = await {entity}Service.GetByIdAsync(id, cancellationToken);
            return current.IsSuccess
                ? FormView(nameof(Delete), {Entity}DetailsViewModel.From(current.Value!), StatusCodes.Status409Conflict)
                : NotFound();
        }

        // The details page of a deleted record no longer exists, so always return to the list.
        return Saved("Record deleted.", Url.Action(nameof(Index))!);
    }

    private static int StatusFor(string errorCode) => errorCode switch
    {
        {Entity}Errors.ConcurrencyConflict or {Entity}Errors.DuplicateCode => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity
    };

    /// <summary>Shows the error next to the field it is about, or in the form summary.</summary>
    private void AddError(string errorCode, string errorMessage)
    {
        var key = errorCode switch
        {
            {Entity}Errors.DuplicateCode => nameof({Entity}FormViewModel.Code),
            _ => string.Empty
        };

        ModelState.AddModelError(key, errorMessage);
    }
}
```

`Models/`

```csharp
/// <summary>Create and edit share this form; display-only values are [BindNever] and reloaded.</summary>
public sealed class {Entity}FormViewModel
{
    [Required]
    [StringLength({Entity}Entity.CodeMaxLength)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength({Entity}Entity.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength({Entity}Entity.DescriptionMaxLength)]
    [DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    [Required]
    public {Entity}Status? Status { get; set; } = {Entity}Status.Active;   // nullable + [Required] → clear message

    /// <summary>Base64 row version captured when the edit form was loaded.</summary>
    public string? RowVersion { get; set; }
}

public sealed record {Entity}DetailsViewModel(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    {Entity}Status Status,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    string RowVersion)
{
    public static {Entity}DetailsViewModel From({Entity}Dto dto) => new(
        dto.Id, dto.Code, dto.Name, dto.Description, dto.Status,
        dto.CreatedAtUtc, dto.CreatedBy, dto.UpdatedAtUtc, dto.UpdatedBy,
        Convert.ToBase64String(dto.RowVersion));
}

/// <summary>The list page; RouteValues keeps the current filters on pager and facet links.</summary>
public sealed record {Entity}ListViewModel(
    PagedResponse<{Entity}ListItemDto> Page,
    {Entity}StatisticsDto Statistics,
    {Entity}ListFilter Filter)
{
    public bool IsFacet({Entity}Status? status = null) => Filter.Status == status;

    public Dictionary<string, string> RouteValues(int? page = null, {Entity}Status? status = null, bool keepFacets = true)
    {
        var values = new Dictionary<string, string>();
        if (Filter.Search is { } search)
        {
            values["search"] = search;
        }

        if ((keepFacets ? status ?? Filter.Status : status) is { } statusValue)
        {
            values["status"] = statusValue.ToString();
        }

        if (page is > 1)
        {
            values["page"] = page.Value.ToString(CultureInfo.InvariantCulture);
        }

        return values;
    }
}

/// <summary>Every enum's display text, CSS modifier and select options — views never ToString() an enum.</summary>
public static class {Entity}Labels
{
    public static string ToLabel({Entity}Status status) => status switch
    {
        {Entity}Status.Active => "Active",
        {Entity}Status.Inactive => "Inactive",
        _ => status.ToString()
    };

    public static string ToCssModifier({Entity}Status status) => status switch
    {
        {Entity}Status.Active => "success",
        _ => "neutral"
    };

    public static IEnumerable<SelectListItem> StatusOptions() =>
        Enum.GetValues<{Entity}Status>().Select(status => new SelectListItem(ToLabel(status), status.ToString()));
}
```

Views: see [web-ui.md](web-ui.md).

---

## Api host — `Features/{Feature}/`

`Contracts/{Entity}Requests.cs` / `{Entity}Responses.cs`

```csharp
public sealed record Create{Entity}Request(
    [Required] string Code,
    [Required] string Name,
    string? Description,
    [Required] {Entity}Status? Status);

public sealed record Update{Entity}Request(
    [Required] string Code,
    [Required] string Name,
    string? Description,
    [Required] {Entity}Status? Status);

/// <summary>No RowVersion in the body — it travels as the ETag header.</summary>
public sealed record {Entity}Response(
    Guid Id, string Code, string Name, string? Description, {Entity}Status Status,
    DateTimeOffset CreatedAtUtc, string? CreatedBy, DateTimeOffset? UpdatedAtUtc, string? UpdatedBy)
{
    public static {Entity}Response From({Entity}Dto dto) => new(
        dto.Id, dto.Code, dto.Name, dto.Description, dto.Status,
        dto.CreatedAtUtc, dto.CreatedBy, dto.UpdatedAtUtc, dto.UpdatedBy);
}

public sealed record {Entity}SummaryResponse(Guid Id, string Code, string Name, {Entity}Status Status)
{
    public static {Entity}SummaryResponse From({Entity}ListItemDto dto) => new(dto.Id, dto.Code, dto.Name, dto.Status);
}

public sealed record {Entity}PageResponse(
    IReadOnlyList<{Entity}SummaryResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record {Entity}StatisticsResponse(int TotalCount, IReadOnlyDictionary<{Entity}Status, int> CountByStatus)
{
    /// <summary>Every status is present, with zero when there are none.</summary>
    public static {Entity}StatisticsResponse From({Entity}StatisticsDto dto) => new(
        dto.TotalCount, Enum.GetValues<{Entity}Status>().ToDictionary(status => status, dto.CountFor));
}
```

`{Feature}Controller.cs`

```csharp
/// <summary>CRUD for {feature}. Reads return an <c>ETag</c>; updates and deletes require it in <c>If-Match</c>.</summary>
[ApiController]
[Route("api/{feature-route}")]
public sealed class {Feature}Controller(I{Entity}Service {entity}Service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<{Entity}PageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] {Entity}Status? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PagedRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await {entity}Service.ListAsync(
            new {Entity}ListFilter(search, status), new PagedRequest(page, pageSize), cancellationToken);
        var paged = result.Value!;

        return Ok(new {Entity}PageResponse(
            paged.Items.Select({Entity}SummaryResponse.From).ToList(),
            paged.Page, paged.PageSize, paged.TotalCount, paged.TotalPages));
    }

    [HttpGet("statistics")]
    [ProducesResponseType<{Entity}StatisticsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Statistics(CancellationToken cancellationToken) =>
        Ok({Entity}StatisticsResponse.From((await {entity}Service.GetStatisticsAsync(cancellationToken)).Value!));

    [HttpGet("{id:guid}")]
    [ReturnsETag(StatusCodes.Status200OK)]
    [ProducesResponseType<{Entity}Response>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await {entity}Service.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return Problem(result.ErrorCode!, result.ErrorMessage!);
        }

        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return Ok({Entity}Response.From(result.Value!));
    }

    [HttpPost]
    [ReturnsETag(StatusCodes.Status201Created)]
    [ProducesResponseType<{Entity}Response>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Create{Entity}Request request, CancellationToken cancellationToken)
    {
        var created = await {entity}Service.CreateAsync(
            new Create{Entity}Dto(request.Code, request.Name, request.Description, request.Status!.Value),
            cancellationToken);
        if (created.IsFailure)
        {
            return Problem(created.ErrorCode!, created.ErrorMessage!);
        }

        var result = await {entity}Service.GetByIdAsync(created.Value, cancellationToken);
        ConcurrencyHeaders.SetETag(Response, result.Value!.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = created.Value }, {Entity}Response.From(result.Value!));
    }

    [HttpPut("{id:guid}")]
    [RequiresIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Update(Guid id, Update{Entity}Request request, CancellationToken cancellationToken)
    {
        if (!ConcurrencyHeaders.TryReadIfMatch(Request, out var rowVersion))
        {
            return PreconditionRequired();
        }

        var result = await {entity}Service.UpdateAsync(
            id,
            new Update{Entity}Dto(request.Code, request.Name, request.Description, request.Status!.Value, rowVersion),
            cancellationToken);

        return result.IsSuccess ? NoContent() : Problem(result.ErrorCode!, result.ErrorMessage!);
    }

    [HttpDelete("{id:guid}")]
    [RequiresIfMatch]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!ConcurrencyHeaders.TryReadIfMatch(Request, out var rowVersion))
        {
            return PreconditionRequired();
        }

        var result = await {entity}Service.DeleteAsync(id, rowVersion, cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.ErrorCode!, result.ErrorMessage!);
    }

    private ObjectResult Problem(string errorCode, string errorMessage)
    {
        var statusCode = errorCode switch
        {
            {Entity}Errors.NotFound => StatusCodes.Status404NotFound,
            {Entity}Errors.Validation => StatusCodes.Status400BadRequest,
            {Entity}Errors.DuplicateCode => StatusCodes.Status409Conflict,
            {Entity}Errors.ConcurrencyConflict => StatusCodes.Status412PreconditionFailed,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = Problem(detail: errorMessage, statusCode: statusCode);
        ((ProblemDetails)problem.Value!).Extensions["code"] = errorCode;
        return problem;
    }

    private ObjectResult PreconditionRequired()
    {
        var problem = Problem(
            detail: "Send the record's current ETag in the If-Match header.",
            statusCode: StatusCodes.Status428PreconditionRequired);
        ((ProblemDetails)problem.Value!).Extensions["code"] = "{Entity}.PreconditionRequired";
        return problem;
    }
}
```

---

## Tests — `tests/{App}.Tests/`

| Folder | File | What it proves | How |
| --- | --- | --- | --- |
| `Domain/` | `{Entity}Tests` | invariants, normalization, transitions | plain xUnit, no mocks |
| `Application/` | `{Entity}ServiceTests` | orchestration: what is loaded, checked, saved, and **not** saved on failure | Moq `I{Entity}Repository`, `FakeTimeProvider` |
| `Integration/` | `{Entity}PersistenceIntegrationTests` | mapping, unique indexes, soft delete, concurrency, audit stamps | real SQL Server via `SqlServerFixture` |
| `Integration/` | `{Entity}ApiTests` | status codes, ProblemDetails `code`, ETag/If-Match | `ApiHostFactory` + HttpClient |
| `Integration/` | `{Entity}WebTests` | pages render from feature folder, PRG, 409/422 re-render | `WebHostFactory`, `AllowAutoRedirect = false` |
| `Web/` | `{Entity}ListViewModelTests` | view-model logic (route values, labels) | plain xUnit |
| `Support/` | `{Entity}TestData` | builders for valid DTOs/entities | static helpers |

```csharp
public sealed class {Entity}ServiceTests
{
    private readonly Mock<I{Entity}Repository> _repository = new();
    private readonly {Entity}Service _service;

    public {Entity}ServiceTests()
    {
        _repository.Setup(r => r.AddAsync(It.IsAny<{Entity}>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _service = new {Entity}Service(_repository.Object);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_code_without_saving()
    {
        _repository.Setup(r => r.CodeExistsAsync("ABC", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.CreateAsync({Entity}TestData.CreateDto(code: " abc "), CancellationToken.None);

        Assert.Equal({Entity}Errors.DuplicateCode, result.ErrorCode);
        _repository.Verify(r => r.AddAsync(It.IsAny<{Entity}>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

[Collection(SqlServerCollection.Name)]
public sealed class {Entity}WebTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private WebHostFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new WebHostFactory(await fixture.CreateDatabaseAsync());
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Index_renders_from_the_feature_folder()
    {
        var html = await _client.GetStringAsync("/{Feature}");
        Assert.Contains("data-live-refresh=\"{Entity}\"", html);
    }
}
```

Posting forms in Web tests: GET the form first, read `__RequestVerificationToken` (and the
`RowVersion` hidden field for edits/deletes), then POST with those values.
