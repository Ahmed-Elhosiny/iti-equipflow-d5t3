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

        // Map the public navigation property normally
        builder.HasMany(x => x.Messages)
            .WithOne()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Explicitly instruct EF Core to use the private '_messages' backing field 
        // for this navigation property. This prevents the In-Memory provider from 
        // throwing DbUpdateConcurrencyException when adding to the IReadOnlyCollection, 
        // and maintains strict DDD encapsulation.
        builder.Navigation(c => c.Messages)
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}