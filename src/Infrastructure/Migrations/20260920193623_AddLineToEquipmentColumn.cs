using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLineToEquipmentColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Line",
                table: "equipment",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Line",
                table: "equipment");
        }
    }
}
