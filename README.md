# Aircraft MRO

The solution targets .NET 10 and uses SQL Server for persistence. A database container
built from `mcr.microsoft.com/mssql/server` is a supported local runtime.

## Local database configuration

Keep the database password outside source control. Configure both ASP.NET Core hosts
with the `ConnectionStrings__AircraftMRO` environment variable or their existing user
secrets IDs. When the application runs on the host and SQL Server publishes port 1433,
a local connection string has this shape:

```text
Server=localhost,1433;Database=AircraftMRO;User ID=sa;Password=<password>;Encrypt=True;TrustServerCertificate=True
```

Apply the schema from the repository root before starting the Web host:

```bash
ConnectionStrings__AircraftMRO='<connection-string>' dotnet ef database update \
  --project src/AircraftMRO.Infrastructure/AircraftMRO.Infrastructure.csproj \
  --startup-project src/AircraftMRO.Web/AircraftMRO.Web.csproj
```

Then run either host with the same connection-string configuration:

```bash
dotnet run --project src/AircraftMRO.Web/AircraftMRO.Web.csproj
dotnet run --project src/AircraftMRO.Api/AircraftMRO.Api.csproj
```

When the application also runs in a container, replace `localhost` with the SQL Server
container or service name on their shared container network. The password supplied in
the connection string must match the container's `MSSQL_SA_PASSWORD`; do not commit it.

## API reference

In Development the API host serves an interactive [Scalar](https://scalar.com) reference at
`/scalar` (for example `http://localhost:5146/scalar`) and the OpenAPI document at
`/openapi/v1.json`. Neither is mapped in other environments. Updates and deletes need the
aircraft's current `ETag` in an `If-Match` header; the reference documents this on each operation.

## Real-time notifications

Every create, update, and delete of an auditable entity is recorded in the `Notifications`
table in the same transaction as the change, from either host. The Web host polls that table
and pushes new rows to browsers over SignalR (`/hubs/notifications`), so changes made through
the API appear live too, typically within a second. Settings live in the `Notifications`
configuration section: `PollInterval` (default `00:00:01`), `BatchSize` (100), and
`ReplayWindow` (`00:00:10`). History is at `/Notifications`. The SignalR browser client is
vendored under `wwwroot/lib/microsoft-signalr` and recorded in `libman.json`.
