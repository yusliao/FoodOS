using FSH.Modules.Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Logistics.Data.Configurations;

public sealed class ShipmentLineLotConfiguration : IEntityTypeConfiguration<ShipmentLineLot>
{
    public void Configure(EntityTypeBuilder<ShipmentLineLot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ShipmentLineLots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Zone).IsRequired().HasMaxLength(16);
        builder.Property(x => x.LotNo).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.HasIndex(x => x.ShipmentLineId);
        builder.Ignore(x => x.DomainEvents);
    }
}
