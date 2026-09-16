using EquipFlow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EquipFlow.Infrastructure.Persistence.Configurations;

public sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("equipment");

        builder.HasKey(equipment => equipment.Id);

        builder.Property(equipment => equipment.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(equipment => equipment.SerialNumber)
            .HasMaxLength(100);
    }
}
