using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public class RunSpendConfiguration : IEntityTypeConfiguration<RunSpend>
{
    public void Configure(EntityTypeBuilder<RunSpend> builder)
    {
        builder.ToTable("RunSpends");
        
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        
        builder.HasIndex(x => x.RunId).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.ReservedAmount)
            .HasConversion(money => money.Amount, amount => Money.FromDecimal(amount))
            .HasPrecision(18, 4);

        builder.Property(x => x.ActualAmount)
            .HasConversion(money => money.Amount, amount => Money.FromDecimal(amount))
            .HasPrecision(18, 4);
            
        builder.Property(x => x.ModelUsed).IsRequired().HasMaxLength(100);
    }
}