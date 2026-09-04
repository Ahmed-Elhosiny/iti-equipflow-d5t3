using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("work_orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Symptom)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.EquipmentName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.EquipmentAssetNumber)
            .HasMaxLength(100);

        builder.Property(x => x.ManualRevision)
            .HasMaxLength(100);

        builder.Property(x => x.Location)
            .HasMaxLength(300);

        builder.Property(x => x.CreatedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(x => x.DecisionBy)
            .HasMaxLength(200);

        builder.Property(x => x.DecisionComment)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasMany(x => x.SafetyPrerequisites)
            .WithOne()
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ApprovalActions)
            .WithOne()
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
