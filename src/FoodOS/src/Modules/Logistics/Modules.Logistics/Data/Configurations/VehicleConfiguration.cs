using FSH.Modules.Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Logistics.Data.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Vehicles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Plate).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.Plate).IsUnique();
        builder.Property(x => x.CompartmentZones).IsRequired().HasMaxLength(64);
        builder.Property(x => x.PayloadKg).HasPrecision(18, 3);
        builder.Ignore(x => x.DomainEvents);
    }
}
