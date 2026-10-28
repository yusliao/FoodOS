using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Catalog.Data.Configurations;

public sealed class CustomerLevelConfiguration : IEntityTypeConfiguration<CustomerLevel>
{
    public void Configure(EntityTypeBuilder<CustomerLevel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("CustomerLevels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
