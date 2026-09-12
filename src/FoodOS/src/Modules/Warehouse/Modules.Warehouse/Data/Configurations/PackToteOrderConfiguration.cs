using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Warehouse.Data.Configurations;

public sealed class PackToteOrderConfiguration : IEntityTypeConfiguration<PackToteOrder>
{
    public void Configure(EntityTypeBuilder<PackToteOrder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PackToteOrders");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => new { x.PackToteId, x.OrderId }).IsUnique();
    }
}
