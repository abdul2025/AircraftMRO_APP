# Shared kernel

Create these once per system, before the first feature. They hold no business logic.
Replace `{App}` with the solution prefix.

## Domain

`Common/Entities/BaseEntity.cs`

```csharp
namespace {App}.Domain.Common.Entities;

public abstract class BaseEntity
{
    /// <summary>Assigned by persistence when the entity is added; empty until then.</summary>
    public Guid Id { get; protected set; }
}
```

`Common/Entities/AuditableEntity.cs`

```csharp
namespace {App}.Domain.Common.Entities;

/// <summary>
/// Base for every aggregate root. Audit and soft-delete values are stamped by Infrastructure
/// when changes are saved; domain and presentation code never set them.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public string? DeletedBy { get; private set; }
}
```

`Common/Entities/IHasDisplayName.cs`

```csharp
namespace {App}.Domain.Common.Entities;

/// <summary>A human-readable name used wherever the entity is mentioned to a person (logs, notifications).</summary>
public interface IHasDisplayName
{
    string DisplayName { get; }
}
```

`Common/Results/Result.cs` and `ResultOfT.cs`

```csharp
namespace {App}.Domain.Common.Results;

public sealed class Result
{
    private Result(bool isSuccess, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    public static Result Success() => new(true, null, null);

    public static Result Failure(string errorCode, string errorMessage) => new(false, errorCode, errorMessage);
}

public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    public static Result<T> Success(T value) => new(true, value, null, null);

    public static Result<T> Failure(string errorCode, string errorMessage) => new(false, default, errorCode, errorMessage);
}
```

Converting a failed `Result` into `Result<T>`: `Result<Guid>.Failure(r.ErrorCode!, r.ErrorMessage!)`.

## Application

`Common/Interfaces/ICurrentUser.cs`

```csharp
namespace {App}.Application.Common.Interfaces;

public interface ICurrentUser
{
    /// <summary>The authenticated user's id, or <c>null</c> when there is no authenticated user.</summary>
    string? UserId { get; }
}
```

`Common/Paging/PagedRequest.cs` and `PagedResponse.cs`

```csharp
namespace {App}.Application.Common.Paging;

public sealed record PagedRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Largest page whose offset still fits in an <see cref="int"/> at the maximum page size.</summary>
    public const int MaxPage = int.MaxValue / MaxPageSize;

    public PagedRequest(int page = 1, int pageSize = DefaultPageSize)
    {
        Page = Math.Clamp(page, 1, MaxPage);
        PageSize = Math.Clamp(pageSize, 1, MaxPageSize);
    }

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
```

`DependencyInjection.cs`

```csharp
namespace {App}.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        // one line per feature:
        // services.AddScoped<I{Entity}Service, {Entity}Service>();

        return services;
    }
}
```

## Infrastructure

`Persistence/{App}DbContext.cs`

```csharp
namespace {App}.Infrastructure.Persistence;

public sealed class {App}DbContext(DbContextOptions<{App}DbContext> options) : DbContext(options)
{
    public const string RowVersionPropertyName = "RowVersion";
    public const int UserIdMaxLength = 450;

    // public DbSet<{Entity}> {Feature} => Set<{Entity}>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // modelBuilder.HasSequence<long>({Entity}Configuration.NumberSequenceName);   // see patterns.md
        modelBuilder.ApplyConfigurationsFromAssembly(typeof({App}DbContext).Assembly);
        ConfigureAuditableEntities(modelBuilder);
    }

    /// <summary>
    /// Every entity deriving from <see cref="AuditableEntity"/> gets the audit columns,
    /// a row-version concurrency token, and a filter that hides soft-deleted rows.
    /// </summary>
    private static void ConfigureAuditableEntities(ModelBuilder modelBuilder)
    {
        var auditableTypes = modelBuilder.Model.GetEntityTypes()
            .Where(type => typeof(AuditableEntity).IsAssignableFrom(type.ClrType) && type.BaseType is null)
            .Select(type => type.ClrType)
            .ToList();

        foreach (var clrType in auditableTypes)
        {
            var entity = modelBuilder.Entity(clrType);

            // Generated on Add as SQL Server-ordered sequential GUIDs, which keeps clustered-index inserts at the end.
            entity.Property(nameof(BaseEntity.Id)).ValueGeneratedOnAdd();
            entity.Property(nameof(AuditableEntity.CreatedAtUtc)).IsRequired();
            entity.Property(nameof(AuditableEntity.CreatedBy)).HasMaxLength(UserIdMaxLength);
            entity.Property(nameof(AuditableEntity.UpdatedBy)).HasMaxLength(UserIdMaxLength);
            entity.Property(nameof(AuditableEntity.DeletedBy)).HasMaxLength(UserIdMaxLength);
            entity.Property(nameof(AuditableEntity.IsDeleted)).HasDefaultValue(false);
            entity.Property<byte[]>(RowVersionPropertyName).IsRowVersion();

            var parameter = Expression.Parameter(clrType, "entity");
            var notDeleted = Expression.Lambda(
                Expression.Not(Expression.Property(parameter, nameof(AuditableEntity.IsDeleted))),
                parameter);
            entity.HasQueryFilter(notDeleted);
        }
    }
}
```

`Auditing/AuditableEntityInterceptor.cs`

```csharp
namespace {App}.Infrastructure.Auditing;

/// <summary>Stamps audit values on every <see cref="AuditableEntity"/> and turns deletes into soft deletes.</summary>
internal sealed class AuditableEntityInterceptor(ICurrentUser currentUser, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditValues(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditValues(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditValues(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        // Optional: build change-log rows here, BEFORE deletes become soft-delete updates (see patterns.md §9).

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    Set(entry, nameof(AuditableEntity.CreatedAtUtc), now);
                    Set(entry, nameof(AuditableEntity.CreatedBy), userId);
                    Set(entry, nameof(AuditableEntity.UpdatedAtUtc), null);
                    Set(entry, nameof(AuditableEntity.UpdatedBy), null);
                    Set(entry, nameof(AuditableEntity.IsDeleted), false);
                    Set(entry, nameof(AuditableEntity.DeletedAtUtc), null);
                    Set(entry, nameof(AuditableEntity.DeletedBy), null);
                    break;

                case EntityState.Modified:
                    ProtectCreationValues(entry);
                    Set(entry, nameof(AuditableEntity.UpdatedAtUtc), now);
                    Set(entry, nameof(AuditableEntity.UpdatedBy), userId);
                    break;

                case EntityState.Deleted:
                    SoftDelete(entry, now, userId);
                    break;
            }
        }
    }

    private static void SoftDelete(EntityEntry<AuditableEntity> entry, DateTimeOffset now, string? userId)
    {
        // Keep the concurrency token the caller expects so a stale delete is still rejected.
        var rowVersion = entry.Property({App}DbContext.RowVersionPropertyName);
        var expectedRowVersion = rowVersion.OriginalValue;

        entry.State = EntityState.Unchanged;
        rowVersion.OriginalValue = expectedRowVersion;

        Set(entry, nameof(AuditableEntity.IsDeleted), true);
        Set(entry, nameof(AuditableEntity.DeletedAtUtc), now);
        Set(entry, nameof(AuditableEntity.DeletedBy), userId);
    }

    private static void ProtectCreationValues(EntityEntry<AuditableEntity> entry)
    {
        foreach (var name in new[] { nameof(AuditableEntity.CreatedAtUtc), nameof(AuditableEntity.CreatedBy) })
        {
            var property = entry.Property(name);
            property.CurrentValue = property.OriginalValue;
            property.IsModified = false;
        }
    }

    private static void Set(EntityEntry entry, string propertyName, object? value) =>
        entry.Property(propertyName).CurrentValue = value;
}
```

`Auditing/CurrentUser.cs`

```csharp
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        }
    }
}
```

`DependencyInjection.cs`

```csharp
namespace {App}.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "{App}";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Required configuration 'ConnectionStrings:{ConnectionStringName}' is missing.");
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<{App}DbContext>((serviceProvider, options) =>
            options
                .UseSqlServer(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        // one line per feature:
        // services.AddScoped<I{Entity}Repository, {Entity}Repository>();

        return services;
    }
}
```

The `.csproj` adds `<InternalsVisibleTo Include="{App}.Tests" />`.

## Web host

`Configuration/FeatureViewLocationExpander.cs`

```csharp
/// <summary>Resolves views from <c>Features/{Controller}/Views/</c> before the default MVC locations.</summary>
public sealed class FeatureViewLocationExpander : IViewLocationExpander
{
    private static readonly string[] FeatureLocations = ["/Features/{1}/Views/{0}.cshtml"];

    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations) =>
        FeatureLocations.Concat(viewLocations);
}
```

`Controllers/ModalFormController.cs`

```csharp
/// <summary>
/// Create, edit, and delete forms render as modal content when requested with the modal header
/// (see <c>wwwroot/js/modal-forms.js</c>) and as full pages otherwise.
/// </summary>
public abstract class ModalFormController : Controller
{
    public const string ModalRequestHeader = "X-Modal-Request";
    public const string ModalReturnUrlHeader = "X-Modal-Return-Url";
    protected const string StatusMessageKey = "StatusMessage";
    protected const string ErrorMessageKey = "ErrorMessage";

    protected bool IsModalRequest => Request.Headers[ModalRequestHeader] == "true";

    /// <summary>The page the modal was opened from, accepted only when it is local to this site.</summary>
    protected string? ModalReturnUrl
    {
        get
        {
            if (!IsModalRequest)
            {
                return null;
            }

            var returnUrl = Request.Headers[ModalReturnUrlHeader].ToString();
            return Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        }
    }

    /// <summary>Renders the form as modal content (partial <c>_{action}Form</c>) or as the full page <c>{action}</c>.</summary>
    protected IActionResult FormView(string action, object model, int statusCode = StatusCodes.Status200OK)
    {
        Response.StatusCode = statusCode;
        return IsModalRequest ? PartialView($"_{action}Form", model) : View(action, model);
    }

    /// <summary>After a successful save: a redirect for full pages, or its URL as JSON for the modal script.</summary>
    protected IActionResult Saved(string message, string redirectUrl)
    {
        TempData[StatusMessageKey] = message;
        return IsModalRequest ? Json(new { redirectUrl }) : Redirect(redirectUrl);
    }

    /// <summary>Decodes the base64 row version a form carried; false when it is missing or malformed.</summary>
    protected static bool TryDecodeRowVersion(string? value, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written == 0)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
    }
}
```

`Features/_ViewStart.cshtml` → `@{ Layout = "_Layout"; }`
`Features/_ViewImports.cshtml` → `@using {App}.Web`, `@using {App}.Web.Models`, `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`

`Views/Shared/_StatusMessages.cshtml`

```cshtml
@if (TempData["StatusMessage"] is string status)
{
    <p class="notice notice--success" role="status">@status</p>
}
@if (TempData["ErrorMessage"] is string error)
{
    <p class="notice notice--danger" role="alert">@error</p>
}
```

`_Layout.cshtml` must contain `<main id="main-content">`, an empty shared modal
`<div class="modal fade" id="app-modal" tabindex="-1" aria-hidden="true"><div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable"><div class="modal-content"></div></div></div>`,
Bootstrap's JS bundle, `~/js/modal-forms.js`, and `@await RenderSectionAsync("Scripts", required: false)`.

`wwwroot/js/modal-forms.js`

```js
// Opens links marked [data-modal] in the shared #app-modal and submits their forms in place.
// The server returns form HTML (initial load or validation errors) or JSON { redirectUrl } on success.
// Without JavaScript the same links open full pages, so this is a progressive enhancement.
(() => {
  "use strict";

  const modalElement = document.getElementById("app-modal");
  if (!modalElement || !window.bootstrap) {
    return;
  }

  const content = modalElement.querySelector(".modal-content");
  const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
  const loadError = "This form could not be loaded. Refresh the page and try again.";
  const saveError = "Your changes could not be saved. Check your connection and try again.";

  const modalHeaders = () => ({
    "X-Modal-Request": "true",
    "X-Modal-Return-Url": window.location.pathname + window.location.search
  });

  const render = (html) => {
    content.innerHTML = html;
    const title = content.querySelector(".modal-title");
    if (title?.id) {
      modalElement.setAttribute("aria-labelledby", title.id);
    }
  };

  const renderError = (message) => {
    const alert = document.createElement("p");
    alert.className = "notice notice--danger";
    alert.setAttribute("role", "alert");
    alert.textContent = message;

    const body = content.querySelector(".modal-body");
    if (body) {
      body.prepend(alert);
      return;
    }

    content.replaceChildren();
    const wrapper = document.createElement("div");
    wrapper.className = "modal-body";
    const close = document.createElement("button");
    close.type = "button";
    close.className = "button button--secondary";
    close.dataset.bsDismiss = "modal";
    close.textContent = "Close";
    wrapper.append(alert, close);
    content.append(wrapper);
  };

  const focusFirstField = () => {
    const target = content.querySelector(".input-validation-error, [aria-invalid='true']")
      ?? content.querySelector(".modal-body input:not([type='hidden']), .modal-body select")
      ?? content.querySelector("[type='submit']");
    target?.focus();
  };

  document.addEventListener("click", async (event) => {
    const link = event.target.closest("a[data-modal]");
    if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey) {
      return;
    }

    event.preventDefault();
    try {
      const response = await fetch(link.href, { headers: modalHeaders() });
      if (!response.ok) {
        throw new Error(String(response.status));
      }

      render(await response.text());
    } catch {
      render("");
      renderError(loadError);
    }

    modal.show();
  });

  modalElement.addEventListener("shown.bs.modal", focusFirstField);
  modalElement.addEventListener("hidden.bs.modal", () => content.replaceChildren());

  content.addEventListener("submit", async (event) => {
    const form = event.target.closest("form[data-modal-form]");
    if (!form) {
      return;
    }

    event.preventDefault();
    const submit = form.querySelector("[type='submit']");
    submit?.setAttribute("disabled", "");
    submit?.setAttribute("aria-busy", "true");

    try {
      const response = await fetch(form.action, { method: "POST", headers: modalHeaders(), body: new FormData(form) });
      const contentType = response.headers.get("content-type") ?? "";

      if (response.ok && contentType.includes("application/json")) {
        const { redirectUrl } = await response.json();
        window.location.assign(redirectUrl);
        return;
      }

      if (contentType.includes("text/html")) {
        render(await response.text());
        focusFirstField();
        return;
      }

      throw new Error(String(response.status));
    } catch {
      renderError(saveError);
      submit?.removeAttribute("disabled");
      submit?.removeAttribute("aria-busy");
    }
  });
})();
```

## Api host

`OpenApi/ConcurrencyHeaders.cs`

```csharp
/// <summary>
/// Row versions travel as strong ETags: reads return one, updates and deletes send it back in <c>If-Match</c>.
/// </summary>
public static class ConcurrencyHeaders
{
    public static void SetETag(HttpResponse response, byte[] rowVersion) =>
        response.Headers.ETag = new EntityTagHeaderValue($"\"{Convert.ToBase64String(rowVersion)}\"").ToString();

    /// <summary>False when <c>If-Match</c> is missing, weak, a list, or not a base64 row version.</summary>
    public static bool TryReadIfMatch(HttpRequest request, out byte[] rowVersion)
    {
        rowVersion = [];
        var header = request.GetTypedHeaders().IfMatch;
        if (header is not [{ IsWeak: false } tag] || tag.Tag.Length < 3)
        {
            return false;
        }

        var value = tag.Tag.Value![1..^1];
        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written == 0)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
    }
}
```

`OpenApi/ConcurrencyHeaderAttributes.cs`

```csharp
/// <summary>The action requires the resource's current ETag in the <c>If-Match</c> header.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresIfMatchAttribute : Attribute;

/// <summary>The action's success response carries the resource's <c>ETag</c> header.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ReturnsETagAttribute(int statusCode) : Attribute
{
    public int StatusCode { get; } = statusCode;
}
```

`OpenApi/OpenApiConfiguration.cs` — `AddApiDocumentation()`:
- `ConfigureHttpJsonOptions` + `JsonStringEnumConverter` (the OpenAPI generator reads these options, not MVC's);
- `AddOpenApi` with a document transformer (title, version, description of ETag/If-Match and the problem-details `code` extension) and an operation transformer that adds a required `If-Match` header parameter for `[RequiresIfMatch]` and an `ETag` response header for `[ReturnsETag(status)]`.

`MapApiDocumentation()`: `MapOpenApi()` + `MapScalarApiReference(...)` with agent, MCP, telemetry and developer tools disabled — called in Development only.

## Tests support

```csharp
public sealed class TestCurrentUser(string? userId = null) : ICurrentUser
{
    public string? UserId { get; set; } = userId;
}

public sealed class FakeTimeProvider(DateTimeOffset? now = null) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now ?? new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>One SQL Server container per test run; each test class migrates its own database.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<string> CreateDatabaseAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"{App}_Test_{Guid.NewGuid():N}"
        }.ConnectionString;

        await using var context = CreateContext(connectionString, new TestCurrentUser(), new FakeTimeProvider());
        await context.Database.MigrateAsync();
        return connectionString;
    }

    public static {App}DbContext CreateContext(string connectionString, ICurrentUser currentUser, TimeProvider timeProvider) =>
        new(new DbContextOptionsBuilder<{App}DbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new AuditableEntityInterceptor(currentUser, timeProvider))
            .Options);
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

public sealed class WebHostFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", connectionString);
    }
}

// Api host: same shape over WebApplicationFactory<ApiHost::Program>
// (test project references the Api with <Aliases>ApiHost</Aliases> because both hosts declare Program).
```
