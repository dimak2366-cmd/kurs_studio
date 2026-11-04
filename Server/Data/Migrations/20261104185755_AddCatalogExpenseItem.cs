using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogExpenseItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_expense_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KosguCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_expense_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_expense_items_Name_KosguCode",
                table: "catalog_expense_items",
                columns: new[] { "Name", "KosguCode" },
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO catalog_expense_items (""Name"", ""KosguCode"", ""IsActive"")
                SELECT DISTINCT ""Name"", ""KosguCode"", TRUE
                FROM expense_items
                WHERE ""Name"" IS NOT NULL AND ""KosguCode"" IS NOT NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_expense_items");
        }
    }
}
