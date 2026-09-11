using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class CartLineConfiguration : IEntityTypeConfiguration<CartLine>
{
    public void Configure(EntityTypeBuilder<CartLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("CartLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.Zone).IsRequired().HasMaxLength(16);
        builder.Ignore(x => x.DomainEvents);
    }
}
