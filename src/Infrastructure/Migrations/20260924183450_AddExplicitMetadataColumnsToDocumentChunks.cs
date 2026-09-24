using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExplicitMetadataColumnsToDocumentChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "equipment_id",
                table: "document_chunks",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "production_line",
                table: "document_chunks",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_equipment_id",
                table: "document_chunks",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_production_line",
                table: "document_chunks",
                column: "production_line");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_document_chunks_equipment_id",
                table: "document_chunks");

            migrationBuilder.DropIndex(
                name: "IX_document_chunks_production_line",
                table: "document_chunks");

            migrationBuilder.DropColumn(
                name: "equipment_id",
                table: "document_chunks");

            migrationBuilder.DropColumn(
                name: "production_line",
                table: "document_chunks");
        }
    }
}
