using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Catalog.Data.Configurations;

public sealed class PriceListLineConfiguration : IEntityTypeConfiguration<PriceListLine>
{
    public void Configure(EntityTypeBuilder<PriceListLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PriceListLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MinQty).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.HasIndex(x => new { x.PriceListId, x.ProductId, x.MinQty }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
