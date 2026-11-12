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

## Work orders

Each aircraft can have many work orders (`/WorkOrders` in the Web host, `/api/work-orders` in the
API). A new work order is always `Open` and gets the next number from the `WorkOrderNumbers`
database sequence (`WO-000001`, …). Completed and cancelled work orders are final.

An aircraft's open work orders decide its status. Every create, priority change, completion, or
cancellation recalculates it in the same transaction:

| Open work orders on the aircraft | Aircraft status |
| --- | --- |
| At least one Critical | Grounded |
| Some, none Critical | Required maintenance |
| None (the last one was completed or cancelled) | Active |

So lowering a work order from Critical to High moves the aircraft from Grounded to Required maintenance
(unless another critical one is open), and raising it back grounds it again. While work orders are
open, the aircraft's status cannot be changed by hand (409). Open work orders cannot be deleted;
complete or cancel them first, so every status change has a recorded reason. Aircraft with open
work orders cannot be deleted, and retired aircraft cannot get new work orders. Every work order
save also writes its aircraft row, so a concurrent change to the aircraft or to another of its work
orders is caught by the row version instead of leaving the status out of step. That case returns
409 with code `WorkOrder.AircraftChanged`: the caller's work order version is still valid, so the
save can simply be retried. A stale work order version still returns 412 `WorkOrder.ConcurrencyConflict`.

A work order is overdue once its due date has passed in the business time zone, set with
`WorkOrders:TimeZone` (an IANA id such as `Asia/Riyadh`; default `UTC`) in both hosts. An unknown
id stops the host at startup.

## Maintenance orders

Each work order can have many maintenance orders (`/MaintenanceOrders` in the Web host,
`/api/maintenance-orders` in the API). A maintenance order has a type (`ACheck`, `BCheck`,
`CCheck`, `DCheck`, `Unscheduled`), a scheduled date, optional notes, and a status:

| Status | Can edit | Can delete |
| --- | --- | --- |
| `Scheduled` (every new order starts here) | yes | yes |
| `InProgress` | yes | no |
| `Completed` (completion time is stamped) | no, final | no, kept as history |

- Maintenance can only be scheduled or changed while its work order is open (409
  `MaintenanceOrder.WorkOrderClosed`).
- **A work order cannot be completed or cancelled while any of its maintenance is `Scheduled` or
  `InProgress`** (409 `WorkOrder.OpenMaintenance`). Complete the maintenance, or delete it if it was
  only scheduled, first.
- **The work order follows its maintenance.** When any maintenance is started (`InProgress`), an
  `Open` or `OnHold` work order moves to `InProgress`. When all of its maintenance is `Completed`
  (by completing the last open one, or by deleting the last scheduled one), the work order is
  `Completed` too, and its aircraft's status is recalculated from its remaining open work orders,
  exactly as when a work order is closed by hand (for example back to `Active`). Editing only the
  type, date, or notes does not touch the work order.
- Scheduling new maintenance, changing a maintenance status, and deleting maintenance all write
  the work order row, so concurrent changes are caught by its row version. A close that missed
  newly scheduled maintenance gets 412 `WorkOrder.ConcurrencyConflict`; a maintenance change that
  missed a work order change (including another maintenance change under the same work order)
  gets 409 `MaintenanceOrder.WorkOrderChanged`. Reload and retry in either case.
- **Known limitation:** changing several maintenance orders under the *same* work order in
  parallel (for example a bulk API import) makes all but one of the simultaneous requests fail
  with 409 `MaintenanceOrder.WorkOrderChanged`. Nothing is lost and the failed requests can be
  retried as-is. Send them one at a time, or retry on that code.
- Work order list and details show how many maintenance orders each work order has
  (`maintenanceCount`, and `completedMaintenanceCount` on details, in the API).
- Maintenance is overdue once its scheduled date has passed (same business time zone as work
  orders) and it is not completed.

## Real-time notifications

Every create, update, and delete of an auditable entity is recorded in the `Notifications`
table in the same transaction as the change, from either host. The Web host polls that table
and pushes new rows to browsers over SignalR (`/hubs/notifications`), so changes made through
the API appear live too, typically within a second. Settings live in the `Notifications`
configuration section: `PollInterval` (default `00:00:01`), `BatchSize` (100), and
`ReplayWindow` (`00:00:10`). History is at `/Notifications`. The SignalR browser client is
vendored under `wwwroot/lib/microsoft-signalr` and recorded in `libman.json`.
