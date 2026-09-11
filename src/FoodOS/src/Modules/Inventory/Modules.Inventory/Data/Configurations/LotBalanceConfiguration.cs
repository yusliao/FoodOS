using FSH.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Inventory.Data.Configurations;

public sealed class LotBalanceConfiguration : IEntityTypeConfiguration<LotBalance>
{
    public void Configure(EntityTypeBuilder<LotBalance> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("LotBalances");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.WarehouseId, x.ZoneId, x.LotId }).IsUnique();
        builder.HasIndex(x => new { x.WarehouseId, x.ProductId, x.ZoneId });
        builder.Property(x => x.OnHand).HasPrecision(18, 4);
        builder.Property(x => x.Reserved).HasPrecision(18, 4);
        builder.Property(x => x.Allocated).HasPrecision(18, 4);
        builder.Property(x => x.Picked).HasPrecision(18, 4);
        builder.Property(x => x.InTransit).HasPrecision(18, 4);
        builder.Property(x => x.Isolated).HasPrecision(18, 4);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Ignore(x => x.Available);
        builder.Ignore(x => x.DomainEvents);
    }
}
