using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParentSmetaIdToSmeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentSmetaId",
                table: "smetas",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_smetas_ParentSmetaId",
                table: "smetas",
                column: "ParentSmetaId");

            migrationBuilder.AddForeignKey(
                name: "FK_smetas_smetas_ParentSmetaId",
                table: "smetas",
                column: "ParentSmetaId",
                principalTable: "smetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_smetas_smetas_ParentSmetaId",
                table: "smetas");

            migrationBuilder.DropIndex(
                name: "IX_smetas_ParentSmetaId",
                table: "smetas");

            migrationBuilder.DropColumn(
                name: "ParentSmetaId",
                table: "smetas");
        }
    }
}
