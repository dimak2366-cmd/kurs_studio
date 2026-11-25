using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class UxActualExpenseDoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LimitReturnComment",
                table: "expense_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LimitStatus",
                table: "expense_items",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_actual_expenses_ExpenseItemId_PeriodId_DocumentNumber",
                table: "actual_expenses",
                columns: new[] { "ExpenseItemId", "PeriodId", "DocumentNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_actual_expenses_ExpenseItemId_PeriodId_DocumentNumber",
                table: "actual_expenses");

            migrationBuilder.DropColumn(
                name: "LimitReturnComment",
                table: "expense_items");

            migrationBuilder.DropColumn(
                name: "LimitStatus",
                table: "expense_items");
        }
    }
}
