using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AircraftMRO.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds Countries (seeded with ISO 3166-1), AircraftManufacturers and AircraftModels, and moves
    /// Aircraft from free-text Manufacturer/Model columns to foreign keys. Existing aircraft, including
    /// soft-deleted ones, are linked to manufacturers and models built from their old text values, so
    /// no data is lost. Hand-edited: the scaffold dropped the text columns without carrying them over.
    /// </summary>
    /// <remarks>
    /// Data statements run through <c>EXEC</c> so an idempotent script still compiles when it refers
    /// to columns that an earlier statement in the same script adds or removes.
    /// </remarks>
    public partial class AddAircraftReferenceData : Migration
    {
        private const string OtherCategory = "7"; // AircraftCategory.Other, frozen for this migration.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Countries, seeded.
            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Code",
                table: "Countries",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Name",
                table: "Countries",
                column: "Name");

            migrationBuilder.Sql(Exec($"""
                DECLARE @now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);
                INSERT INTO [Countries] ([Id], [Name], [Code], [IsActive], [CreatedAtUtc], [IsDeleted])
                SELECT NEWID(), seed.[Name], seed.[Code], 1, @now, 0
                FROM (VALUES {Values(SeedCountries.Select(c => (c.Name, c.Code)))}) AS seed([Name], [Code]);
                """));

            // 2. Manufacturers and models.
            migrationBuilder.CreateTable(
                name: "AircraftManufacturers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AircraftManufacturers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AircraftManufacturers_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AircraftModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManufacturerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModelCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AircraftCategory = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AircraftModels", x => x.Id);
                    table.UniqueConstraint("AK_AircraftModels_ManufacturerId_Id", x => new { x.ManufacturerId, x.Id });
                    table.ForeignKey(
                        name: "FK_AircraftModels_AircraftManufacturers_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "AircraftManufacturers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AircraftManufacturers_Code",
                table: "AircraftManufacturers",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AircraftManufacturers_CountryId",
                table: "AircraftManufacturers",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_AircraftManufacturers_Name",
                table: "AircraftManufacturers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AircraftModels_ManufacturerId_ModelCode",
                table: "AircraftModels",
                columns: new[] { "ManufacturerId", "ModelCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // 3. New aircraft columns, nullable until the backfill has filled them.
            migrationBuilder.AddColumn<Guid>(
                name: "ManufacturerId",
                table: "Aircraft",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModelId",
                table: "Aircraft",
                type: "uniqueidentifier",
                nullable: true);

            // 4. One manufacturer per distinct name. The code is the name's upper-case letters and
            //    digits (MFR when nothing usable is left), with a number appended when two collide.
            migrationBuilder.Sql(Exec($"""
                DECLARE @now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);
                WITH names AS (
                    SELECT DISTINCT LTRIM(RTRIM([Manufacturer])) AS [Name] FROM [Aircraft]
                ),
                stripped AS (
                    SELECT [Name], {StripToCode("[Name]")} AS [Raw] FROM names
                ),
                based AS (
                    SELECT [Name],
                        CASE WHEN [Raw] = N'' OR [Raw] COLLATE Latin1_General_BIN LIKE N'%[^A-Z0-9]%'
                             THEN N'MFR' ELSE LEFT([Raw], 7) END AS [Base]
                    FROM stripped
                ),
                numbered AS (
                    SELECT [Name], [Base], ROW_NUMBER() OVER (PARTITION BY [Base] ORDER BY [Name]) AS [N] FROM based
                )
                INSERT INTO [AircraftManufacturers] ([Id], [Name], [Code], [CountryId], [IsActive], [CreatedAtUtc], [IsDeleted])
                SELECT NEWID(),
                    n.[Name],
                    CASE WHEN n.[N] = 1 THEN n.[Base] ELSE n.[Base] + CAST(n.[N] AS nvarchar(3)) END,
                    country.[Id],
                    1,
                    @now,
                    0
                FROM numbered n
                OUTER APPLY (
                    SELECT TOP (1) known.[CountryCode]
                    FROM (VALUES {Values(KnownManufacturerCountries.Select(k => (k.NamePrefix, k.CountryCode)))})
                        AS known([NamePrefix], [CountryCode])
                    WHERE n.[Name] LIKE known.[NamePrefix] + N'%'
                    ORDER BY LEN(known.[NamePrefix]) DESC
                ) AS match
                LEFT JOIN [Countries] country ON country.[Code] = match.[CountryCode] AND country.[IsDeleted] = 0;
                """));

            // 5. One model per distinct (manufacturer, model name), category Other, with a code built
            //    the same way and numbered per manufacturer when two collide.
            migrationBuilder.Sql(Exec($"""
                DECLARE @now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);
                WITH pairs AS (
                    SELECT DISTINCT m.[Id] AS [ManufacturerId], LTRIM(RTRIM(a.[Model])) AS [Name]
                    FROM [Aircraft] a
                    JOIN [AircraftManufacturers] m ON m.[Name] = LTRIM(RTRIM(a.[Manufacturer]))
                ),
                stripped AS (
                    SELECT [ManufacturerId], [Name], {StripToCode("[Name]")} AS [Raw] FROM pairs
                ),
                based AS (
                    SELECT [ManufacturerId], [Name],
                        CASE WHEN [Raw] = N'' OR [Raw] COLLATE Latin1_General_BIN LIKE N'%[^A-Z0-9]%'
                             THEN N'MODEL' ELSE LEFT([Raw], 17) END AS [Base]
                    FROM stripped
                ),
                numbered AS (
                    SELECT [ManufacturerId], [Name], [Base],
                        ROW_NUMBER() OVER (PARTITION BY [ManufacturerId], [Base] ORDER BY [Name]) AS [N]
                    FROM based
                )
                INSERT INTO [AircraftModels] ([Id], [ManufacturerId], [Name], [ModelCode], [AircraftCategory], [IsActive], [CreatedAtUtc], [IsDeleted])
                SELECT NEWID(),
                    n.[ManufacturerId],
                    n.[Name],
                    CASE WHEN n.[N] = 1 THEN n.[Base] ELSE n.[Base] + CAST(n.[N] AS nvarchar(3)) END,
                    {OtherCategory},
                    1,
                    @now,
                    0
                FROM numbered n;
                """));

            migrationBuilder.Sql(Exec("""
                UPDATE a
                SET a.[ManufacturerId] = m.[Id], a.[ModelId] = model.[Id]
                FROM [Aircraft] a
                JOIN [AircraftManufacturers] m ON m.[Name] = LTRIM(RTRIM(a.[Manufacturer]))
                JOIN [AircraftModels] model ON model.[ManufacturerId] = m.[Id] AND model.[Name] = LTRIM(RTRIM(a.[Model]));
                """));

            // 6. Every aircraft now has both values; fails here, rolling back, if the backfill missed one.
            migrationBuilder.AlterColumn<Guid>(
                name: "ManufacturerId",
                table: "Aircraft",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ModelId",
                table: "Aircraft",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // 7. Retire the text columns.
            migrationBuilder.DropIndex(
                name: "IX_Aircraft_Manufacturer_SerialNumber",
                table: "Aircraft");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "Aircraft");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "Aircraft");

            // 8. Keys and indexes on the new columns.
            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_ManufacturerId_ModelId",
                table: "Aircraft",
                columns: new[] { "ManufacturerId", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_ManufacturerId_SerialNumber",
                table: "Aircraft",
                columns: new[] { "ManufacturerId", "SerialNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Aircraft_AircraftManufacturers_ManufacturerId",
                table: "Aircraft",
                column: "ManufacturerId",
                principalTable: "AircraftManufacturers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Aircraft_AircraftModels_ManufacturerId_ModelId",
                table: "Aircraft",
                columns: new[] { "ManufacturerId", "ModelId" },
                principalTable: "AircraftModels",
                principalColumns: new[] { "ManufacturerId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the text columns from the names the aircraft point at, then drop the new shape.
            // Manufacturers, models and countries that no aircraft uses are not kept.
            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "Aircraft",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "Aircraft",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(Exec("""
                UPDATE a
                SET a.[Manufacturer] = m.[Name], a.[Model] = model.[Name]
                FROM [Aircraft] a
                JOIN [AircraftManufacturers] m ON m.[Id] = a.[ManufacturerId]
                JOIN [AircraftModels] model ON model.[Id] = a.[ModelId];
                """));

            migrationBuilder.AlterColumn<string>(
                name: "Manufacturer",
                table: "Aircraft",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "Aircraft",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Aircraft_AircraftManufacturers_ManufacturerId",
                table: "Aircraft");

            migrationBuilder.DropForeignKey(
                name: "FK_Aircraft_AircraftModels_ManufacturerId_ModelId",
                table: "Aircraft");

            migrationBuilder.DropIndex(
                name: "IX_Aircraft_ManufacturerId_ModelId",
                table: "Aircraft");

            migrationBuilder.DropIndex(
                name: "IX_Aircraft_ManufacturerId_SerialNumber",
                table: "Aircraft");

            migrationBuilder.DropColumn(
                name: "ManufacturerId",
                table: "Aircraft");

            migrationBuilder.DropColumn(
                name: "ModelId",
                table: "Aircraft");

            migrationBuilder.DropTable(
                name: "AircraftModels");

            migrationBuilder.DropTable(
                name: "AircraftManufacturers");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_Manufacturer_SerialNumber",
                table: "Aircraft",
                columns: new[] { "Manufacturer", "SerialNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <summary>Runs <paramref name="sql"/> as dynamic SQL, so it is compiled only when it executes.</summary>
        private static string Exec(string sql) => $"EXEC(N'{sql.Replace("'", "''")}');";

        /// <summary>A T-SQL VALUES list of string pairs, with quotes escaped.</summary>
        private static string Values(System.Collections.Generic.IEnumerable<(string First, string Second)> rows) =>
            string.Join(", ", rows.Select(row => $"(N'{row.First.Replace("'", "''")}', N'{row.Second.Replace("'", "''")}')"));

        /// <summary>Upper-cases <paramref name="column"/> and drops the separators a name usually contains.</summary>
        private static string StripToCode(string column) =>
            "UPPER(" + new[] { " ", "-", ".", ",", "'", "&", "/", "(", ")" }
                .Aggregate(column, (expression, separator) =>
                    $"REPLACE({expression}, N'{separator.Replace("'", "''")}', N'')") + ")";
    }
}
