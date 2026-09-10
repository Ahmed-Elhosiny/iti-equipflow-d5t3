using EquipFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.DocumentId)
            .IsRequired();

        builder.Property(chunk => chunk.Content)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(chunk => chunk.TokenCount)
            .IsRequired();

        builder.Property(chunk => chunk.Embedding)
            .IsRequired()
            .HasColumnType("vector(1536)");

        builder.OwnsOne(chunk => chunk.Metadata, metadata =>
        {
            metadata.Property(value => value.Source)
                .IsRequired()
                .HasMaxLength(1000)
                .HasColumnName("metadata_source");

            metadata.Property(value => value.Section)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("metadata_section");

            metadata.Property(value => value.PageNumber)
                .HasColumnName("metadata_page_number");

            metadata.Property(value => value.Version)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("metadata_version");

            metadata.Property(value => value.Format)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("metadata_format");
        });

        builder.HasIndex(chunk => chunk.DocumentId);
    }
}