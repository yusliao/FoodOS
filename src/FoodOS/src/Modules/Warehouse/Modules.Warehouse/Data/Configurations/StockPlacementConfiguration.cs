using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Warehouse.Data.Configurations;

public sealed class StockPlacementConfiguration : IEntityTypeConfiguration<StockPlacement>
{
    public void Configure(EntityTypeBuilder<StockPlacement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("StockPlacements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.HasIndex(x => new { x.LotId, x.LocationId }).IsUnique();
    }
}
