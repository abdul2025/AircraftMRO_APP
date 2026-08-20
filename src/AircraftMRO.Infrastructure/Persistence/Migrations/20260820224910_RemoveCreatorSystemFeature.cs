using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AircraftMRO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCreatorSystemFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SystemFeatures",
                keyColumn: "Id",
                keyValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SystemFeatures",
                columns: new[] { "Id", "ActionName", "Code", "ControllerName", "Description", "DisplayOrder", "IconKey", "IsVisible", "StatusText", "Title" },
                values: new object[] { 5, null, "creator", null, "Create and manage maintenance records, work orders, and compliance documentation.", 50, "creator", true, "Coming soon", "Creator" });
        }
    }
}
