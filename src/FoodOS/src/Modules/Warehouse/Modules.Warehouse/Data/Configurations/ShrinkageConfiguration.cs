using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Warehouse.Data.Configurations;

public sealed class ShrinkageConfiguration : IEntityTypeConfiguration<Shrinkage>
{
    public void Configure(EntityTypeBuilder<Shrinkage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Shrinkages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Zone).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(128);
        builder.Property(x => x.PhotoFileIds).HasMaxLength(1024);
        builder.HasIndex(x => x.LotId);
        builder.Ignore(x => x.DomainEvents);
    }
}
