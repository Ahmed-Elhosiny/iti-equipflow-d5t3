using EquipFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(document => document.Type)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(document => document.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(document => document.CreatedAt)
            .IsRequired();

        builder.Property(document => document.UpdatedAt)
            .IsRequired();

        builder.OwnsOne(document => document.Metadata, metadata =>
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

        builder.HasMany(document => document.Chunks)
            .WithOne()
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(document => document.Chunks)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}