using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Catalog.Data.Configurations;

public sealed class ProductContractLockConfiguration : IEntityTypeConfiguration<ProductContractLock>
{
    public void Configure(EntityTypeBuilder<ProductContractLock> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ProductContractLocks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.HasIndex(x => new { x.CustomerOrgId, x.ProductId }).IsUnique();
        builder.HasIndex(x => x.Until);
        builder.Ignore(x => x.DomainEvents);
    }
}
