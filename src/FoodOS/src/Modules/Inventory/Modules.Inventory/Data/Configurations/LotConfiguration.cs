using FSH.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Inventory.Data.Configurations;

public sealed class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Lots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LotNo).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => new { x.LotNo, x.ProductId }).IsUnique();
        builder.HasIndex(x => x.ExpiryDate);
        builder.Property(x => x.Origin).HasMaxLength(128);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Ignore(x => x.DomainEvents);
    }
}
