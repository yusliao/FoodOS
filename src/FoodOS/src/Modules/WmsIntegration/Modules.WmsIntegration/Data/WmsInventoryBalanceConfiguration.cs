using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using FSH.Modules.WmsIntegration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.WmsIntegration.Data;

public sealed class WmsInventoryBalanceConfiguration : IEntityTypeConfiguration<WmsInventoryBalance>
{
    public void Configure(EntityTypeBuilder<WmsInventoryBalance> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("InventoryBalances", WmsIntegrationDbContext.Schema);
        builder.IsMultiTenant();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ConnectionId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ExternalObjectId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.WarehouseId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OwnerId).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Uom).HasMaxLength(30).IsRequired();
        builder.Property(x => x.LotNumber).HasMaxLength(100);
        builder.Property(x => x.OnHandQuantity).HasPrecision(19, 6);
        builder.Property(x => x.AllocatedQuantity).HasPrecision(19, 6);
        builder.Property(x => x.AvailableQuantity).HasPrecision(19, 6);
        builder.Property(x => x.QuarantinedQuantity).HasPrecision(19, 6);
        builder.HasIndex(x => new { x.Provider, x.ConnectionId, x.ExternalObjectId }).IsUnique();
        builder.HasIndex(x => new { x.Provider, x.ConnectionId, x.WarehouseId, x.OwnerId, x.Sku, x.Uom });
        builder.Ignore(x => x.DomainEvents);
    }
}
