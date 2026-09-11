using FSH.Modules.Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Logistics.Data.Configurations;

public sealed class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Routes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
        builder.Property(x => x.StoreSequence).IsRequired().HasMaxLength(4000);
        builder.Ignore(x => x.DomainEvents);
    }
}
