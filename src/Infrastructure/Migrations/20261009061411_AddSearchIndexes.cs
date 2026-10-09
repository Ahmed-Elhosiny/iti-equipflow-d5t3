using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_document_chunks_embedding_hnsw ON document_chunks USING hnsw (embedding vector_cosine_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_document_chunks_content_gin ON document_chunks USING gin (to_tsvector('simple', content));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_document_chunks_embedding_hnsw;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_document_chunks_content_gin;");
        }
    }
}
