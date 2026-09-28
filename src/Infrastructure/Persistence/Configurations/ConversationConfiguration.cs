using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EquipFlow.Domain.Entities;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.UpdatedAtUtc);

        builder.HasMany(x => x.Messages)
            .WithOne()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}