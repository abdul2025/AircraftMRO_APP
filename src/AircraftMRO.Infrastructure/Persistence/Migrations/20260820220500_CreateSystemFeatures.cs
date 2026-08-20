using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AircraftMRO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateSystemFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    IconKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ControllerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ActionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StatusText = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemFeatures", x => x.Id);
                    table.CheckConstraint("CK_SystemFeatures_Destination_Complete", "([ControllerName] IS NULL AND [ActionName] IS NULL) OR ([ControllerName] IS NOT NULL AND [ActionName] IS NOT NULL)");
                    table.CheckConstraint("CK_SystemFeatures_DisplayOrder_NonNegative", "[DisplayOrder] >= 0");
                });

            migrationBuilder.InsertData(
                table: "SystemFeatures",
                columns: new[] { "Id", "ActionName", "Code", "ControllerName", "Description", "DisplayOrder", "IconKey", "IsVisible", "StatusText", "Title" },
                values: new object[,]
                {
                    { 1, null, "aircraft", null, "Manage aircraft profiles, registration details, fleet status, and technical records.", 10, "aircraft", true, "Coming soon", "Aircraft" },
                    { 2, null, "work-orders", null, "Plan, assign, and track maintenance work from discovery through release to service.", 20, "work-order", true, "Coming soon", "Work Orders" },
                    { 3, null, "maintenance-planning", null, "Coordinate scheduled tasks, due dates, labor, tooling, and material requirements.", 30, "maintenance", true, "Coming soon", "Maintenance Planning" },
                    { 4, null, "compliance-records", null, "Keep maintenance history, airworthiness evidence, and audit-ready records together.", 40, "compliance", true, "Coming soon", "Compliance & Records" },
                    { 5, null, "creator", null, "Create and manage maintenance records, work orders, and compliance documentation.", 50, "creator", true, "Coming soon", "Creator" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemFeatures_Code",
                table: "SystemFeatures",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemFeatures");
        }
    }
}
