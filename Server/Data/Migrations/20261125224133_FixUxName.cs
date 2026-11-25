using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixUxName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_actual_expenses_ExpenseItemId_PeriodId_DocumentNumber",
                table: "actual_expenses",
                newName: "ux_actual_expenses_doc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ux_actual_expenses_doc",
                table: "actual_expenses",
                newName: "IX_actual_expenses_ExpenseItemId_PeriodId_DocumentNumber");
        }
    }
}
