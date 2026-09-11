using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Warehouse.Data.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Locations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(x => x.DomainEvents);
    }
}
