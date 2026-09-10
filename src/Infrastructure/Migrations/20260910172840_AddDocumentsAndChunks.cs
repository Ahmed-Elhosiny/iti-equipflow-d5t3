using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace EquipFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentsAndChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    metadata_source = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    metadata_section = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    metadata_page_number = table.Column<int>(type: "integer", nullable: true),
                    metadata_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    metadata_format = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    metadata_source = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    metadata_section = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    metadata_page_number = table.Column<int>(type: "integer", nullable: true),
                    metadata_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    metadata_format = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_chunks_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_DocumentId",
                table: "document_chunks",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "documents");
        }
    }
}
