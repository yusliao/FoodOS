using FSH.Modules.Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Logistics.Data.Configurations;

public sealed class TemperatureReadingConfiguration : IEntityTypeConfiguration<TemperatureReading>
{
    public void Configure(EntityTypeBuilder<TemperatureReading> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TemperatureReadings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Compartment).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Celsius).HasPrecision(6, 2);
        builder.HasIndex(x => new { x.VehicleId, x.RecordedAt });
        builder.Ignore(x => x.DomainEvents);
    }
}
