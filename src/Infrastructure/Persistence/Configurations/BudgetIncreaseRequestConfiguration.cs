using EquipFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public class BudgetIncreaseRequestConfiguration : IEntityTypeConfiguration<BudgetIncreaseRequest>
{
    public void Configure(EntityTypeBuilder<BudgetIncreaseRequest> builder)
    {
        builder.ToTable("BudgetIncreaseRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Reason).IsRequired().HasMaxLength(1000);
        builder.Property(r => r.RequestedAmount).HasColumnType("numeric(18,2)");
        builder.Property(r => r.Status).HasConversion<string>();
    }
}
