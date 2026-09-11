using FSH.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Inventory.Data.Configurations;

public sealed class TemperatureZoneConfiguration : IEntityTypeConfiguration<TemperatureZone>
{
    public void Configure(EntityTypeBuilder<TemperatureZone> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TemperatureZones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.WarehouseId, x.Kind }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
