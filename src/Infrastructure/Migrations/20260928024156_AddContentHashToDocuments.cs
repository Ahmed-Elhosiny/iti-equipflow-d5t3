using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentHashToDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "documents",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "documents");
        }
    }
}
