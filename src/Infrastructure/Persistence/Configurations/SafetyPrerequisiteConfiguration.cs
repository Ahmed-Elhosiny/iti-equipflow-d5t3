using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EquipFlow.Domain.Entities;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public class SafetyPrerequisiteConfiguration : IEntityTypeConfiguration<SafetyPrerequisite>
{
    public void Configure(EntityTypeBuilder<SafetyPrerequisite> builder)
    {
        builder.ToTable("safety_prerequisites");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.CompletedBy)
            .HasMaxLength(200);

        builder.Property(x => x.CompletionNote)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.WorkOrderId);
        builder.HasIndex(x => x.SortOrder);
    }
}
