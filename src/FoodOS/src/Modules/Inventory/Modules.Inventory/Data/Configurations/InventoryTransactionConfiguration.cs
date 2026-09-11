using FSH.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Inventory.Data.Configurations;

public sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("InventoryTransactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.FromBucket).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.ToBucket).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.RefType).HasMaxLength(32);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.RefType, x.RefId });
        builder.Ignore(x => x.DomainEvents);
    }
}
