using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public class UserBudgetConfiguration : IEntityTypeConfiguration<UserBudget>
{
    public void Configure(EntityTypeBuilder<UserBudget> builder)
    {
        builder.ToTable("UserBudgets");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.Property(x => x.TotalLimit)
            .HasConversion(
                money => money.Amount,
                amount => Money.FromDecimal(amount))
            .HasPrecision(18, 4);

        builder.Property(x => x.ConsumedAmount)
            .HasConversion(
                money => money.Amount,
                amount => Money.FromDecimal(amount))
            .HasPrecision(18, 4);

        builder.HasMany(x => x.Reservations)
            .WithOne()
            .HasForeignKey("UserBudgetId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Reservations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}