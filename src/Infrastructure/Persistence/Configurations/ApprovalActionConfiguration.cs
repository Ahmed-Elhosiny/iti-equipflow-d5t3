using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EquipFlow.Domain.Entities;
using EquipFlow.Domain.Enums;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public class ApprovalActionConfiguration : IEntityTypeConfiguration<ApprovalAction>
{
    public void Configure(EntityTypeBuilder<ApprovalAction> builder)
    {
        builder.ToTable("approval_actions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActionType)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(x => x.ActorUserId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Comment)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.WorkOrderId);
        builder.HasIndex(x => x.OccurredAtUtc);
    }
}
