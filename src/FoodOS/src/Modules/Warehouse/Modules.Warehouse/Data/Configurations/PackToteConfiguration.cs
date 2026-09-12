using FSH.Modules.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Warehouse.Data.Configurations;

public sealed class PackToteConfiguration : IEntityTypeConfiguration<PackTote>
{
    public void Configure(EntityTypeBuilder<PackTote> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PackTotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sscc).IsRequired().HasMaxLength(48);
        builder.HasIndex(x => x.Sscc).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.OrderIds);
    }
}
