using FSH.Modules.Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Logistics.Data.Configurations;

public sealed class ReturnOnTruckConfiguration : IEntityTypeConfiguration<ReturnOnTruck>
{
    public void Configure(EntityTypeBuilder<ReturnOnTruck> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ReturnsOnTruck");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(256);
        builder.HasIndex(x => x.ShipmentId);
        builder.HasIndex(x => x.OrderId);
        builder.Ignore(x => x.DomainEvents);
    }
}
