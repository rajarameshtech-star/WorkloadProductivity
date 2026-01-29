using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkloadProductivity.Migrations
{
    /// <inheritdoc />
    public partial class addedReasonColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PredicatedAt",
                table: "TaskPredictions",
                newName: "PredictedAt");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "TaskStateHistories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "TaskStateHistories");

            migrationBuilder.RenameColumn(
                name: "PredictedAt",
                table: "TaskPredictions",
                newName: "PredicatedAt");
        }
    }
}
