# Starting a new system

Order: scaffold → shared kernel → first migration → features one at a time (simplest first).

## 1. Scaffold

```bash
dotnet new globaljson --sdk-version 10.0.300 --roll-forward latestPatch
dotnet new sln -n {App} --format slnx
dotnet new classlib -n {App}.Domain         -o src/{App}.Domain
dotnet new classlib -n {App}.Application    -o src/{App}.Application
dotnet new classlib -n {App}.Infrastructure -o src/{App}.Infrastructure
dotnet new mvc      -n {App}.Web            -o src/{App}.Web                     # if a UI is needed
dotnet new webapi   -n {App}.Api            -o src/{App}.Api --use-controllers   # if an API is needed
dotnet new xunit    -n {App}.Tests          -o tests/{App}.Tests
```

Add each project to the solution with `dotnet sln add <path>`, then remove the template's
`Class1.cs`, `WeatherForecast*` and unused sample views.

## 2. Project references (dependencies point inward only)

```
{App}.Application    -> {App}.Domain
{App}.Infrastructure -> {App}.Application, {App}.Domain   (+ <FrameworkReference Include="Microsoft.AspNetCore.App" />)
{App}.Web / {App}.Api -> {App}.Application, {App}.Infrastructure
{App}.Tests          -> all projects; the Api reference gets <Aliases>ApiHost</Aliases>
                        when both hosts exist (each declares a Program class)
```

Packages:

| Project | Packages |
| --- | --- |
| Application | `Microsoft.Extensions.DependencyInjection.Abstractions` |
| Infrastructure | `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design` (PrivateAssets all) |
| Web | `Microsoft.EntityFrameworkCore.Design` (startup project for migrations) |
| Api | `Microsoft.AspNetCore.OpenApi`, `Microsoft.OpenApi`, `Scalar.AspNetCore` |
| Tests | `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Moq`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.MsSql`, `coverlet.collector` |

Every project: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`,
`<TargetFramework>net10.0</TargetFramework>`. Infrastructure: `<InternalsVisibleTo Include="{App}.Tests" />`.
Tests: `<Using Include="Xunit" />`, SDK `Microsoft.NET.Sdk.Web`.

## 3. Shared kernel

Create everything in [shared-kernel.md](shared-kernel.md), in its order (Domain → Application →
Infrastructure → hosts → test support).

## 4. Host Program.cs

Web:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.Configure<RazorViewEngineOptions>(options =>
    options.ViewLocationExpanders.Add(new FeatureViewLocationExpander()));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Pin the culture so number/date binding and display don't depend on the host's regional settings.
var appCulture = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(appCulture),
    SupportedCultures = [appCulture],
    SupportedUICultures = [appCulture]
});

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();

app.Run();

public partial class Program;   // lets WebApplicationFactory<Program> find the host
```

Api:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiDocumentation();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// OpenAPI document at /openapi/v1.json and the Scalar UI at /scalar — Development only.
if (app.Environment.IsDevelopment())
{
    app.MapApiDocumentation();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
```

Authentication/authorization is added per system; `ICurrentUser` reads the `NameIdentifier`
claim, so audit columns fill in as soon as authentication is configured.

## 5. Configuration and secrets

- Connection string key `ConnectionStrings:{App}` (`DependencyInjection.ConnectionStringName`).
  Supply it with `ConnectionStrings__{App}` or `dotnet user-secrets` in each host; never commit it.
- Local SQL Server can run in a container (`mcr.microsoft.com/mssql/server`).
- Feature options per [patterns.md §10](patterns.md#10-feature-options), validated at startup.
- `.gitignore` covers `bin/`, `obj/`, `.vs/`, `.idea/`, `*.user`, `appsettings.*.local.json`, publish output.

## 6. First migration and run

```bash
dotnet ef migrations add Initial \
  --project src/{App}.Infrastructure/{App}.Infrastructure.csproj \
  --startup-project src/{App}.Web/{App}.Web.csproj \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/{App}.Infrastructure/{App}.Infrastructure.csproj \
  --startup-project src/{App}.Web/{App}.Web.csproj

dotnet run --project src/{App}.Web/{App}.Web.csproj
```

(Use the Api project as `--startup-project` when there is no Web host.)

## 7. Feature order

1. Reference/master data with plain CRUD first ([feature-templates.md](feature-templates.md)).
2. Records that reference them (option lists, pattern 7).
3. Records with children, workflows, numbering, derived parent state (patterns 1–4).
4. Statistics, live updates, reporting last.

Each feature is finished across **all** layers and tests before the next one starts.
README documents each feature's business rules as a table of rules and outcomes.
