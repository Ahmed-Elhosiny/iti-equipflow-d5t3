using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public class BudgetReservationConfiguration : IEntityTypeConfiguration<BudgetReservation>
{
    public void Configure(EntityTypeBuilder<BudgetReservation> builder)
    {
        builder.ToTable("BudgetReservations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EstimatedCost)
            .HasConversion(
                money => money.Amount,
                amount => Money.FromDecimal(amount))
            .HasPrecision(18, 4);

        builder.Property(x => x.CreatedAt);
    }
}